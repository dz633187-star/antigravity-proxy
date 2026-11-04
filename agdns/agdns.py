#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AGDNS - локальный DNS для Antigravity.

Зачем: провайдер/роутер подменяет DNS-ответы для доменов Google
(cloudcode-pa.googleapis.com -> 127.65.71.1), из-за чего Antigravity
не может подключиться. Этот сервер слушает 127.0.0.1:53 и ::1:53,
а запросы отправляет по DNS-over-HTTPS (Google / Cloudflare / AdGuard).
Ответы с подменой (127.0.0.0/8, 0.0.0.0) отбрасываются.

Только стандартная библиотека Python 3.8+.
"""

from __future__ import annotations

import argparse
import http.client
import ipaddress
import json
import logging
import os
import socket
import socketserver
import ssl
import struct
import sys
import threading
import time
from concurrent.futures import FIRST_COMPLETED, ThreadPoolExecutor, wait
from logging.handlers import RotatingFileHandler
from urllib.parse import urlparse

APP_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_PATH = os.path.join(APP_DIR, "config.json")
LOG_PATH = os.path.join(APP_DIR, "agdns.log")

QTYPE_A = 1
QTYPE_AAAA = 28
MAX_UDP_PAYLOAD = 1232

DEFAULT_CONFIG = {
    "listen": ["127.0.0.1:53", "[::1]:53"],
    "doh": [
        {"name": "google", "url": "https://dns.google/dns-query",
         "ips": ["8.8.8.8", "8.8.4.4"]},
        {"name": "cloudflare", "url": "https://cloudflare-dns.com/dns-query",
         "ips": ["1.1.1.1", "1.0.0.1"]},
        {"name": "adguard", "url": "https://dns.adguard-dns.com/dns-query",
         "ips": ["94.140.14.14", "94.140.15.15"]},
    ],
    "plain_fallback": ["8.8.8.8", "1.1.1.1"],
    "local_dns": [],
    "cache_ttl_min": 30,
    "cache_ttl_max": 3600,
    "overrides": {},
    "timeout": 3.0,
}

log = logging.getLogger("agdns")


# ---------------------------------------------------------------- DNS helpers

def skip_name(msg: bytes, off: int) -> int:
    """Пропустить DNS-имя (с поддержкой указателей сжатия)."""
    while True:
        if off >= len(msg):
            raise ValueError("truncated name")
        length = msg[off]
        if length == 0:
            return off + 1
        if length & 0xC0 == 0xC0:
            if off + 1 >= len(msg):
                raise ValueError("truncated pointer")
            return off + 2
        if length & 0xC0:
            raise ValueError("bad label")
        off += length + 1


def read_name(msg: bytes, off: int) -> tuple[str, int]:
    labels = []
    jumped = False
    end = off
    seen = 0
    while True:
        if off >= len(msg):
            raise ValueError("truncated name")
        length = msg[off]
        if length == 0:
            off += 1
            if not jumped:
                end = off
            break
        if length & 0xC0 == 0xC0:
            if off + 1 >= len(msg):
                raise ValueError("truncated pointer")
            ptr = ((length & 0x3F) << 8) | msg[off + 1]
            if not jumped:
                end = off + 2
                jumped = True
            off = ptr
            seen += 1
            if seen > 64:
                raise ValueError("pointer loop")
            continue
        labels.append(msg[off + 1:off + 1 + length].decode("latin-1"))
        off += length + 1
    return ".".join(labels), end


def question_end(msg: bytes) -> int:
    _, off = read_name(msg, 12)
    return off + 4


def parse_question(msg: bytes) -> tuple[str, int, int]:
    name, off = read_name(msg, 12)
    qtype, qclass = struct.unpack("!HH", msg[off:off + 4])
    return name, qtype, qclass


def parse_response(msg: bytes) -> dict:
    if len(msg) < 12:
        raise ValueError("short message")
    _qid, flags, qd, an, _ns, _ar = struct.unpack("!HHHHHH", msg[:12])
    off = 12
    for _ in range(qd):
        off = skip_name(msg, off) + 4
    qend = off
    answers = []
    for _ in range(an):
        off = skip_name(msg, off)
        if off + 10 > len(msg):
            break
        rtype, _rclass, ttl, rdlen = struct.unpack("!HHIH", msg[off:off + 10])
        off += 10
        rdata = msg[off:off + rdlen]
        off += rdlen
        answers.append((rtype, ttl, rdata))
    return {"rcode": flags & 0xF, "answers": answers, "qend": qend}


def is_poisoned(parsed: dict) -> bool:
    """Ответ содержит петлевой/нулевой адрес — типичная подмена РКН/провайдера."""
    for rtype, _ttl, rdata in parsed["answers"]:
        if rtype == QTYPE_A and len(rdata) == 4:
            ip = ipaddress.IPv4Address(rdata)
            if ip.is_loopback or ip.is_unspecified or ip.is_multicast:
                return True
        elif rtype == QTYPE_AAAA and len(rdata) == 16:
            ip6 = ipaddress.IPv6Address(rdata)
            if ip6.is_loopback or ip6.is_unspecified:
                return True
    return False


def build_answer(wire: bytes, ips: list[str], ttl: int = 120) -> bytes:
    """Собрать ответ A/AAAA из списка адресов (для overrides)."""
    qid = struct.unpack("!H", wire[:2])[0]
    name, qtype, qclass = parse_question(wire)
    qend = question_end(wire)
    body = wire[12:qend]
    count = 0
    for ip in ips:
        if qtype == QTYPE_A:
            try:
                packed = socket.inet_pton(socket.AF_INET, ip)
            except OSError:
                continue
            rtype, rdlen = QTYPE_A, 4
        elif qtype == QTYPE_AAAA:
            try:
                packed = socket.inet_pton(socket.AF_INET6, ip)
            except OSError:
                continue
            rtype, rdlen = QTYPE_AAAA, 16
        else:
            continue
        body += b"\xc0\x0c" + struct.pack("!HHIH", rtype, qclass, ttl, rdlen) + packed
        count += 1
    flags = 0x8180 if count else 0x8183
    header = struct.pack("!HHHHHH", qid, flags, 1, count, 0, 0)
    return header + (body if count else wire[12:qend])


def make_truncated(wire: bytes) -> bytes:
    qid = struct.unpack("!H", wire[:2])[0]
    flags = struct.unpack("!H", wire[2:4])[0] | 0x0200
    try:
        qend = question_end(wire)
    except Exception:
        qend = len(wire)
    return struct.pack("!HHHHHH", qid, flags, 1, 0, 0, 0) + wire[12:qend]


# ---------------------------------------------------------------- upstreams

class DohUpstream:
    """Один DoH-сервер. Ходим по IP, чтобы не зависеть от системного DNS."""

    def __init__(self, name: str, url: str, ips: list[str], timeout: float):
        self.name = name
        self.url = url
        self.ips = list(ips)
        parsed = urlparse(url)
        self.host = parsed.hostname
        self.port = parsed.port or 443
        self.path = parsed.path or "/dns-query"
        self.timeout = timeout
        self._fails = 0
        self._last_fail = 0.0
        self._lock = threading.Lock()

    def available(self) -> bool:
        with self._lock:
            if self._fails >= 3 and time.time() - self._last_fail < 60:
                return False
        return True

    def _mark(self, ok: bool) -> None:
        with self._lock:
            if ok:
                self._fails = 0
            else:
                self._fails += 1
                self._last_fail = time.time()

    def _request(self, ip: str, wire: bytes, timeout: float) -> bytes:
        ctx = ssl.create_default_context()
        raw = socket.create_connection((ip, self.port), timeout=timeout)
        try:
            raw.settimeout(timeout)
            tls = ctx.wrap_socket(raw, server_hostname=self.host)
        except Exception:
            raw.close()
            raise
        conn = http.client.HTTPSConnection(self.host, self.port, timeout=timeout, context=ctx)
        conn.sock = tls
        try:
            conn.request("POST", self.path, body=wire, headers={
                "content-type": "application/dns-message",
                "accept": "application/dns-message",
                "user-agent": "agdns/1.0",
            })
            resp = conn.getresponse()
            if resp.status != 200:
                raise IOError("HTTP %s" % resp.status)
            return resp.read()
        finally:
            try:
                conn.close()
            except Exception:
                pass

    def query(self, wire: bytes) -> bytes:
        if not self.available():
            raise IOError("temporarily disabled")
        last: Exception | None = None
        for ip in self.ips:
            try:
                data = self._request(ip, wire, self.timeout)
            except Exception as exc:  # noqa: BLE001 - нужен любой сбой
                last = exc
                continue
            self._mark(True)
            return data
        self._mark(False)
        raise last or IOError("no address")


def plain_query(server: str, wire: bytes, timeout: float = 2.0) -> bytes:
    """Обычный UDP-запрос (последний резерв)."""
    fam = socket.AF_INET6 if ":" in server else socket.AF_INET
    with socket.socket(fam, socket.SOCK_DGRAM) as sock:
        sock.settimeout(timeout)
        sock.sendto(wire, (server, 53))
        data, _ = sock.recvfrom(65535)
    return data


# ---------------------------------------------------------------- resolver

class Resolver:
    def __init__(self, cfg: dict):
        self.cfg = cfg
        self.timeout = float(cfg.get("timeout", 3.0))
        self.cache: dict[tuple, tuple[float, bytes, int]] = {}
        self.cache_lock = threading.Lock()
        self.pool = ThreadPoolExecutor(max_workers=24, thread_name_prefix="doh")
        self.upstreams = [
            DohUpstream(u.get("name", u["url"]), u["url"], u.get("ips", []), self.timeout)
            for u in cfg.get("doh", [])
        ]
        self.plain_servers = list(cfg.get("plain_fallback", []))
        self.local_servers = list(cfg.get("local_dns", []))
        self.overrides = {k.lower(): v for k, v in (cfg.get("overrides") or {}).items()}
        self.stats = {"queries": 0, "cache_hits": 0, "doh_ok": 0, "plain_ok": 0,
                      "poison_dropped": 0, "failed": 0}
        self._stats_lock = threading.Lock()

    # -- public

    def resolve(self, wire: bytes, udp: bool = True) -> bytes | None:
        try:
            name, qtype, qclass = parse_question(wire)
        except Exception:
            log.debug("не разобрал запрос: %s", wire[:16].hex())
            return None
        log.debug("запрос %s тип %s", name, qtype)
        self._bump("queries")
        key = (name.lower(), qtype, qclass)

        override = self.overrides.get(name.lower())
        if override and qtype in (QTYPE_A, QTYPE_AAAA):
            return build_answer(wire, override, ttl=300)

        now = time.time()
        with self.cache_lock:
            hit = self.cache.get(key)
        if hit and hit[0] > now:
            self._bump("cache_hits")
            return self._finish(self._from_cache(wire, hit[1], hit[2]), udp)

        data = self._query_doh(wire)
        if data is None:
            data = self._query_plain(wire, self.plain_servers, "plain_ok")
        if data is None:
            data = self._query_plain(wire, self.local_servers, "local_ok")
        if data is None:
            self._bump("failed")
            return self._finish(self._servfail(wire), udp)

        try:
            parsed = parse_response(data)
        except Exception:
            parsed = None
        if parsed is not None:
            ttl = self._ttl(parsed)
            with self.cache_lock:
                if len(self.cache) > 8192:
                    self._prune(now)
                self.cache[key] = (now + ttl, data, parsed["qend"])
        return self._finish(self._with_id(data, wire), udp)

    # -- internals

    def _bump(self, key: str) -> None:
        with self._stats_lock:
            self.stats[key] = self.stats.get(key, 0) + 1

    @staticmethod
    def _with_id(data: bytes, wire: bytes) -> bytes:
        return wire[:2] + data[2:]

    @staticmethod
    def _from_cache(wire: bytes, data: bytes, cached_qend: int) -> bytes:
        try:
            qend = question_end(wire)
        except Exception:
            return Resolver._with_id(data, wire)
        if qend == cached_qend:
            return Resolver._with_id(data, wire)
        # имя в запросе другой длины — отдаём как есть с новым ID
        return Resolver._with_id(data, wire)

    @staticmethod
    def _servfail(wire: bytes) -> bytes:
        qid = struct.unpack("!H", wire[:2])[0]
        try:
            qend = question_end(wire)
        except Exception:
            qend = len(wire)
        return struct.pack("!HHHHHH", qid, 0x8182, 1, 0, 0, 0) + wire[12:qend]

    def _finish(self, data: bytes, udp: bool) -> bytes:
        if udp and len(data) > MAX_UDP_PAYLOAD:
            return make_truncated(data[:12] + data[12:])
        return data

    def _ttl(self, parsed: dict) -> float:
        ttls = [ttl for _t, ttl, _r in parsed["answers"] if ttl > 0]
        ttl = min(ttls) if ttls else 60
        return max(self.cfg.get("cache_ttl_min", 30),
                   min(ttl, self.cfg.get("cache_ttl_max", 3600)))

    def _prune(self, now: float) -> None:
        dead = [k for k, v in self.cache.items() if v[0] <= now]
        for k in dead:
            self.cache.pop(k, None)

    def _valid(self, data: bytes) -> bool:
        try:
            parsed = parse_response(data)
        except Exception:
            return False
        if parsed["rcode"] not in (0, 3):
            return False
        if is_poisoned(parsed):
            self._bump("poison_dropped")
            return False
        return True

    def _query_doh(self, wire: bytes) -> bytes | None:
        futures = [self.pool.submit(u.query, wire) for u in self.upstreams if u.available()]
        if not futures:
            futures = [self.pool.submit(u.query, wire) for u in self.upstreams]
        pending = set(futures)
        deadline = time.time() + self.timeout + 1.0
        try:
            while pending:
                left = deadline - time.time()
                if left <= 0:
                    break
                done, pending = wait(pending, timeout=left, return_when=FIRST_COMPLETED)
                for fut in done:
                    try:
                        data = fut.result()
                    except Exception:
                        continue
                    if self._valid(data):
                        self._bump("doh_ok")
                        return data
        finally:
            for fut in pending:
                fut.cancel()
        return None

    def _query_plain(self, wire: bytes, servers: list[str], counter: str) -> bytes | None:
        for server in servers:
            try:
                data = plain_query(server, wire, timeout=min(self.timeout, 2.0))
            except Exception:
                continue
            if self._valid(data):
                self._bump(counter)
                return data
        return None


# ---------------------------------------------------------------- servers

class UdpHandler(socketserver.BaseRequestHandler):
    def handle(self) -> None:
        data, sock = self.request
        resp = self.server.resolver.resolve(data, udp=True)
        if resp:
            try:
                sock.sendto(resp, self.client_address)
            except OSError:
                pass


class TcpHandler(socketserver.BaseRequestHandler):
    def handle(self) -> None:
        try:
            head = self._recv_exact(2)
            if not head:
                return
            length = struct.unpack("!H", head)[0]
            data = self._recv_exact(length)
            if not data:
                return
            resp = self.server.resolver.resolve(data, udp=False)
            if resp:
                self.request.sendall(struct.pack("!H", len(resp)) + resp)
        except OSError:
            pass

    def _recv_exact(self, n: int) -> bytes:
        buf = b""
        while len(buf) < n:
            chunk = self.request.recv(n - len(buf))
            if not chunk:
                return b""
            buf += chunk
        return buf


class UdpServer(socketserver.ThreadingUDPServer):
    allow_reuse_address = True
    daemon_threads = True


class TcpServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


def parse_listen(entry: str) -> tuple[str, int]:
    if entry.startswith("["):
        host, _, port = entry[1:].partition("]:")
        return host, int(port)
    host, _, port = entry.rpartition(":")
    return host or "127.0.0.1", int(port)


def serve(cfg: dict) -> None:
    resolver = Resolver(cfg)
    servers = []
    for entry in cfg.get("listen", DEFAULT_CONFIG["listen"]):
        host, port = parse_listen(entry)
        family = socket.AF_INET6 if ":" in host else socket.AF_INET
        kwargs = {"address_family": family} if family == socket.AF_INET6 else {}
        try:
            udp = type("U", (UdpServer,), kwargs)((host, port), UdpHandler)
            tcp = type("T", (TcpServer,), kwargs)((host, port), TcpHandler)
        except OSError as exc:
            log.error("не удалось занять %s: %s", entry, exc)
            continue
        udp.resolver = resolver
        tcp.resolver = resolver
        servers.append(udp)
        servers.append(tcp)
        log.info("слушаю %s (UDP+TCP)", entry)

    if not servers:
        log.error("ни один адрес не занят, выходим")
        sys.exit(2)

    threads = []
    for srv in servers:
        th = threading.Thread(target=srv.serve_forever, daemon=True)
        th.start()
        threads.append(th)

    log.info("AGDNS запущен, upstreams: %s",
             ", ".join(u.name for u in resolver.upstreams))
    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        log.info("остановка")


# ---------------------------------------------------------------- selftest

def selftest(port: int) -> int:
    names = ["cloudcode-pa.googleapis.com", "daily-cloudcode-pa.googleapis.com",
             "antigravity.google", "accounts.google.com", "ya.ru"]

    def ask(name: str, qtype: int = QTYPE_A) -> list[str]:
        qid = int(time.time()) & 0xFFFF
        query = struct.pack("!HHHHHH", qid, 0x0100, 1, 0, 0, 0)
        for part in name.split("."):
            query += bytes([len(part)]) + part.encode()
        query += b"\x00" + struct.pack("!HH", qtype, 1)
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.settimeout(6)
            sock.sendto(query, ("127.0.0.1", port))
            data, _ = sock.recvfrom(65535)
        parsed = parse_response(data)
        out = []
        for rtype, _ttl, rdata in parsed["answers"]:
            if rtype == QTYPE_A and len(rdata) == 4:
                out.append(str(ipaddress.IPv4Address(rdata)))
        return out

    failed = 0
    for name in names:
        try:
            ips = ask(name)
        except Exception as exc:  # noqa: BLE001
            print("FAIL %-42s %s" % (name, exc))
            failed += 1
            continue
        bad = [ip for ip in ips if ipaddress.IPv4Address(ip).is_loopback]
        status = "OK  " if ips and not bad else "FAIL"
        if not ips or bad:
            failed += 1
        print("%s %-42s %s" % (status, name, ", ".join(ips) or "нет ответа"))
    return 1 if failed else 0


# ---------------------------------------------------------------- main

def setup_logging(verbose: bool) -> None:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    handlers: list[logging.Handler] = []
    try:
        handlers.append(RotatingFileHandler(LOG_PATH, maxBytes=1_000_000,
                                            backupCount=3, encoding="utf-8"))
    except OSError:
        pass
    handlers.append(logging.StreamHandler(sys.stdout))
    logging.basicConfig(
        level=logging.DEBUG if verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        handlers=handlers,
    )


def _as_list(value) -> list:
    if value is None:
        return []
    if isinstance(value, str):
        return [value]
    return list(value)


def normalize(cfg: dict) -> dict:
    for key in ("listen", "plain_fallback", "local_dns"):
        cfg[key] = _as_list(cfg.get(key))
    cfg["doh"] = [
        dict(item, ips=_as_list(item.get("ips")))
        for item in _as_list(cfg.get("doh"))
        if isinstance(item, dict) and item.get("url")
    ]
    overrides = cfg.get("overrides") or {}
    cfg["overrides"] = {str(k).lower(): _as_list(v) for k, v in overrides.items()}
    return cfg


def load_config(path: str = CONFIG_PATH) -> dict:
    cfg = json.loads(json.dumps(DEFAULT_CONFIG))
    try:
        with open(path, encoding="utf-8-sig") as fh:
            user = json.load(fh)
        if isinstance(user, dict):
            cfg.update(user)
    except FileNotFoundError:
        pass
    except Exception as exc:  # noqa: BLE001
        print("config.json не прочитан: %s" % exc, file=sys.stderr)
    return normalize(cfg)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="AGDNS - локальный DNS для Antigravity")
    parser.add_argument("--config", default=CONFIG_PATH)
    parser.add_argument("--selftest", type=int, metavar="PORT",
                        help="проверить уже запущенный сервер на порту")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args(argv)

    if args.selftest:
        return selftest(args.selftest)

    setup_logging(args.verbose)
    cfg = load_config(args.config)
    log.info("конфиг: %s", args.config)
    serve(cfg)
    return 0


if __name__ == "__main__":
    sys.exit(main())
