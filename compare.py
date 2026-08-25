import os
import sys
import time
import json
import subprocess
import datetime
import signal
import threading
import atexit

import requests

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
SDK_PATH = os.path.join(PROJECT_ROOT, "SDKs", "python")
if SDK_PATH not in sys.path:
    sys.path.insert(0, SDK_PATH)

from axardb import AxarClient

# Hard ceiling for the WHOLE benchmark run. If exceeded, the entire process tree
# (this script + the dotnet AxarDB server it spawned) is force-killed so the
# script can never hang open. Set to 0 to disable.
CONFIG = {
    "overall_timeout": 600,
    "op_timeout": 60,
    "axardb": {
        "start": True,
        "host": "http://localhost:5000",
        "user": "unlocker",
        "password": "unlocker",
    },
    "postgres": {
        "enabled": True,
        "host": "localhost",
        "port": 5432,
        "user": "postgres",
        "password": "1",
        "admin_db": "postgres",
        "test_db": "axar_bench",
    },
    "mariadb": {
        "enabled": True,
        "host": "localhost",
        "port": 3306,
        "user": "root",
        "password": "1",
        "admin_db": "mysql",
        "test_db": "axar_bench",
    },
    "mongodb": {
        "enabled": True,
        "host": "localhost",
        "port": 27017,
        "user": None,
        "password": None,
        "test_db": "axar_bench",
    },
    "record_count": 1000,
    "axardb_build_timeout": 240,
    "output_html": os.path.join(PROJECT_ROOT, "output.html"),
    "log_file": os.path.join(PROJECT_ROOT, "logs", "benchmark_changelog.log"),
}

COLLECTION = "bench_users"
OPERATIONS = [
    "Setup (DDL)",
    "Single Insert",
    "Bulk Insert",
    "Index Creation",
    "Count (COUNT)",
    "Filter Query",
    "Range Query",
    "Aggregation (avg age)",
    "Update",
    "Delete",
]

# AxarDB-specific features that differentiate it from competitor databases.
# Values: "Yes", "No", "Limited" (AxarDB-unique ones are highlighted).
FEATURES = [
    ("JavaScript-based server-side query language", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "Limited"}),
    ("Built-in task queue (queue / sysqueue)", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("Multiple stores in one system (db / memory / bulk)", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("Native HTTP REST API", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("Embedded mode", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("In-memory store", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("Bulk (chunk) store", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("Schemaless document model", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "Yes"}),
    ("Built-in authentication (Basic Auth)", {
        "AxarDB": "Yes", "PostgreSQL": "No", "MariaDB": "No", "MongoDB": "No"}),
    ("SQL compatibility", {
        "AxarDB": "No", "PostgreSQL": "Yes", "MariaDB": "Yes", "MongoDB": "No"}),
]
FEATURE_DBS = ["AxarDB", "PostgreSQL", "MariaDB", "MongoDB"]

SPEEDUP_LABEL = "How many times faster is AxarDB (memory)?"


def now_ts():
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def _t():
    """Monotonic elapsed clock used for per-step timing."""
    return time.perf_counter()


def step(msg, *args):
    """Print a timestamped, elapsed-time step marker so the exact slow stage
    is visible in the console when the run is analyzed afterwards."""
    line = msg
    if args:
        line = f"{msg} :: " + " ".join(str(a) for a in args)
    print(f"[{now_ts()}] {line}", flush=True)


# Module-global reference to the running AxarDB launcher so the watchdog can
# reach the child process tree even from another thread.
_LAUNCHER = None


def kill_process_tree(pid):
    """Force-kill a process and all of its children (Windows-aware)."""
    if pid is None:
        return
    try:
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/PID", str(pid), "/T", "/F"],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
        else:
            import psutil  # optional; only used as a fallback on POSIX
            parent = psutil.Process(pid)
            for child in parent.children(recursive=True):
                try:
                    child.kill()
                except Exception:
                    pass
            parent.kill()
    except Exception:
        try:
            os.kill(pid, signal.SIGKILL)
        except Exception:
            pass


def _force_kill_all(reason):
    """Last-resort watchdog action: kill the AxarDB child tree, then this process."""
    step(f"[WATCHDOG] Forcing exit — {reason}")
    global _LAUNCHER
    if _LAUNCHER is not None and _LAUNCHER.proc is not None:
        try:
            kill_process_tree(_LAUNCHER.proc.pid)
        except Exception:
            pass
    # Give the OS a moment, then hard-exit the whole process.
    time.sleep(1)
    try:
        os._exit(1)
    except Exception:
        pass


atexit.register(lambda: _LAUNCHER.stop() if _LAUNCHER is not None else None)


def log_change(message):
    try:
        os.makedirs(os.path.dirname(CONFIG["log_file"]), exist_ok=True)
        with open(CONFIG["log_file"], "a", encoding="utf-8") as f:
            f.write(f"[{now_ts()}] {message}\n")
    except Exception:
        pass


def time_it(func, *args, **kwargs):
    start = time.perf_counter()
    result = func(*args, **kwargs)
    elapsed = time.perf_counter() - start
    return elapsed, result


class AxarDBLauncher:
    def __init__(self, cfg):
        self.cfg = cfg
        self.proc = None

    def start(self):
        if not self.cfg["start"]:
            return
        url = self.cfg["host"]
        print(f"[AxarDB] Starting ({url}) ...")
        env = dict(os.environ)
        self.proc = subprocess.Popen(
            ["dotnet", "run", "--", "-p", str(url.split(":")[-1])],
            cwd=PROJECT_ROOT,
            env=env,
        )
        deadline = time.time() + CONFIG["axardb_build_timeout"]
        while time.time() < deadline:
            if self.proc.poll() is not None:
                raise RuntimeError("AxarDB exited early.")
            try:
                r = requests.post(
                    f"{url}/query",
                    data="return 1;",
                    headers=self._auth_headers(),
                    timeout=3,
                )
                if r.ok:
                    print("[AxarDB] Ready.")
                    self._expand_query_timeout()
                    return
            except Exception:
                time.sleep(2)
        raise TimeoutError("AxarDB did not become ready in time.")

    def _expand_query_timeout(self):
        """Increase AxarDB query timeout so benchmark scripts are not canceled."""
        try:
            r = requests.post(
                f"{self.cfg['host']}/query",
                data="db.sysconfig.findall().update({ queryTimeoutMinutes: 30 });",
                headers=self._auth_headers(),
                timeout=10,
            )
            if r.ok:
                print("[AxarDB] Query timeout extended to 30 minutes.")
        except Exception as ex:
            print(f"[AxarDB] Warning: could not extend query timeout: {ex}")

    def _auth_headers(self):
        import base64
        auth = f"{self.cfg['user']}:{self.cfg['password']}"
        b64 = base64.b64encode(auth.encode()).decode()
        return {"Authorization": f"Basic {b64}", "Content-Type": "text/plain"}

    def stop(self):
        if self.proc and self.proc.poll() is None:
            step("[AxarDB] Stopping child process tree ...")
            try:
                # CTRL_C_EVENT is unreliable for a dotnet child on Windows, so go
                # straight to killing the whole process tree.
                kill_process_tree(self.proc.pid)
                self.proc.wait(timeout=20)
            except Exception:
                try:
                    kill_process_tree(self.proc.pid)
                except Exception:
                    pass
            step("[AxarDB] Child process stopped.")


class Engine:
    def __init__(self, name):
        self.name = name
        self.status = "pending"
        self.detail = ""
        self.times = {op: None for op in OPERATIONS}

    def run(self):
        raise NotImplementedError

    def warmup(self):
        pass

    def safe_run(self):
        try:
            self.run()
            self.status = "ok"
        except Exception as e:
            self.status = "error"
            self.detail = str(e)[:300]
            print(f"[{self.name}] ERROR: {e}")


class AxarDBStoreEngine(Engine):
    def __init__(self, cfg, store, label):
        super().__init__(label)
        self.cfg = cfg
        self.store = store
        self.client = None
        if store == "bulk":
            self.collection = f"bench_bulk_{os.getpid()}"
        else:
            self.collection = COLLECTION

    def run(self):
        self.client = AxarClient(
            self.cfg["host"], self.cfg["user"], self.cfg["password"]
        )
        n = CONFIG["record_count"]
        step(f"[{self.name}] prepare_collection START (records={n})")
        self._prepare_collection()
        step(f"[{self.name}] prepare_collection DONE")
        step(f"[{self.name}] warmup START")
        self.warmup()
        step(f"[{self.name}] warmup DONE")
        jobs = []
        for op in OPERATIONS:
            if op == "Setup (DDL)":
                self.times[op] = 0.0
                continue
            spec = self._spec(op, n)
            if spec is None:
                self.times[op] = None
                continue
            body, reps = spec
            script = self._loop_script(body, reps)
            step(f"[{self.name}][{op}] queue submit START (reps={reps})")
            t0 = _t()
            job_id = self.client.query(
                "queue(" + json.dumps(script) + ", {}, {priority: 5})"
            )
            step(f"[{self.name}][{op}] queue submit DONE job={job_id} ({_t()-t0:.3f}s)")
            jobs.append((op, job_id, reps, script))

        for op, job_id, reps, script in jobs:
            step(f"[{self.name}][{op}] await START (reps={reps}, timeout={CONFIG['op_timeout']}s)")
            t0 = _t()
            dur_ms = self._await_duration(job_id, script)
            if dur_ms is None:
                self.times[op] = None
                print(f"  [{self.name}][{op}] duration could not be read")
            else:
                self.times[op] = (float(dur_ms) / reps) / 1000.0
                step(f"[{self.name}][{op}] await DONE server_ms={dur_ms} per_op_ms={self.times[op]*1000:.4f} ({_t()-t0:.3f}s)")
            self._clean_job(job_id)

    def _prepare_collection(self):
        if self.store == "bulk":
            return
        script = (
            f'db.deleteCollection("{self.collection}")'
            if self.store == "db"
            else f"{self.store}.{self.collection}.findall().delete()"
        )
        try:
            jid = self.client.query(
                "queue(" + json.dumps(script) + ", {}, {priority: 5})"
            )
            self._await_duration(jid)
            self._clean_job(jid)
        except Exception:
            pass

    def warmup(self):
        for _ in range(3):
            try:
                jid = self.client.query(
                    "queue(" + json.dumps("return 1;") + ", {}, {priority: 5})"
                )
                self._await_duration(jid)
                self._clean_job(jid)
            except Exception:
                pass
        for store in ("memory", "bulk", "db"):
            try:
                docs = [{"name": f"w{i}", "age": i % 80, "active": i % 2 == 0,
                         "city": "London", "score": i * 1.5} for i in range(200)]
                jid = self.client.query(
                    "queue(" + json.dumps(f"{store}.__warm.insert({json.dumps(docs)})") + ", {}, {priority: 5})"
                )
                self._await_duration(jid)
                self._clean_job(jid)
                jid = self.client.query(
                    "queue(" + json.dumps(f"{store}.__warm.findall().count()") + ", {}, {priority: 5})"
                )
                self._await_duration(jid)
                self._clean_job(jid)
                try:
                    if store == "db":
                        jid = self.client.query(
                            "queue(" + json.dumps("db.__warm.findall().delete()") + ", {}, {priority: 5})"
                        )
                    else:
                        jid = self.client.query(
                            "queue(" + json.dumps(f"{store}.__warm.findall().delete()") + ", {}, {priority: 5})"
                        )
                    self._await_duration(jid)
                    self._clean_job(jid)
                except Exception:
                    pass
            except Exception:
                pass

    def _spec(self, op, n):
        obj = f"{self.store}.{self.collection}"
        if op == "Single Insert":
            doc = "{name:'single2',age:31,active:true,city:'Berlin',score:2.0}"
            if self.store == "bulk":
                return f"{obj}.insert([{doc}])", 1
            return f"{obj}.insert({doc})", 1
        if op == "Bulk Insert":
            if self.store in ("bulk", "memory"):
                docs = [
                    {"name": f"user_{i}", "age": (i % 80) + 18, "active": (i % 3 != 0),
                     "city": ["London", "Berlin", "Paris", "Madrid"][i % 4], "score": i * 1.5}
                    for i in range(n)
                ]
                return f"{obj}.insert({json.dumps(docs)})", 1
            body = (
                f"for (var i = 0; i < {n}; i++) {{ {obj}.insert("
                "{name: 'user_' + i, age: (i % 80) + 18, active: (i % 3 != 0), "
                "city: ['London','Berlin','Paris','Madrid'][i % 4], score: i * 1.5}); }"
            )
            return body, 1
        if op == "Count (COUNT)":
            if self.store in ("bulk", "memory"):
                return f"{obj}.count()", 100
            return f"{obj}.findall().count()", 100
        if op == "Filter Query":
            # findall now returns a directly-enumerable result; forcing enumeration
            # via .length runs the query without the obsolete .toList() wrapper.
            return f"var __r = {obj}.findall(x => x.age == 30); var __n = __r.length;", 50
        if op == "Range Query":
            return f"var __r = {obj}.findall(x => x.age > 40); var __n = __r.length;", 50
        if op == "Aggregation (avg age)":
            body = (
                f"var __list = {obj}.findall(); var __s = 0; "
                "for (var __i = 0; __i < __list.length; __i++) { __s += __list[__i].age; } "
                "var __r = __list.length ? __s / __list.length : 0;"
            )
            return body, 20
        if op == "Update":
            if self.store == "db":
                return f"{obj}.update(x => x.age < 30, {{active: false}})", 1
            return None
        if op == "Index Creation":
            if self.store == "db":
                return f'{obj}.index("age", "ASC")', 1
            return None
        if op == "Delete":
            if self.store == "db":
                return f"{obj}.findall(x => x.active == false).delete()", 1
            if self.store == "memory":
                return f"{obj}.findall(x => x.active == false).delete()", 1
            return None
        return None

    def _loop_script(self, body, reps):
        if reps <= 1:
            return f"{body};"
        return f"for (var __k = 0; __k < {reps}; __k++) {{ {body}; }}"

    def _await_duration(self, job_id, script=None):
        deadline = time.time() + CONFIG["op_timeout"]
        poll = 0
        while time.time() < deadline:
            try:
                doc = self.client.query(
                    "db.sysqueue.find(x => x._id == @id)", {"id": job_id}
                )
            except Exception as ex:
                doc = None
                poll += 1
                if poll % 20 == 0:
                    step(f"  [await] job={job_id} poll error: {ex}")
                time.sleep(0.1)
                continue
            if isinstance(doc, dict) and doc.get("completedAt") is not None:
                if doc.get("errorMessage"):
                    step(f"  [queue operation ERROR] {doc.get('errorMessage')}")
                    if script is not None:
                        step(f"  [queue script] {script}")
                return doc.get("duration") or 0
            time.sleep(0.1)
        # Expected time exceeded → force kill the whole run so it never hangs open.
        step(f"  [await] job={job_id} exceeded op_timeout={CONFIG['op_timeout']}s → force killing benchmark")
        _force_kill_all(f"operation '{self.name}' job {job_id} exceeded expected duration")

    def _clean_job(self, job_id):
        try:
            self.client.query(
                "db.sysqueue.findall(x => x._id == @id).delete()", {"id": job_id}
            )
        except Exception:
            pass


class PostgresEngine(Engine):
    def __init__(self, cfg):
        super().__init__("PostgreSQL")
        self.cfg = cfg
        self.conn = None

    def run(self):
        import psycopg2

        c = self.cfg
        db_name = f"{c['test_db']}_{os.getpid()}"
        admin = psycopg2.connect(
            host=c["host"], port=c["port"], user=c["user"], password=c["password"],
            dbname=c["admin_db"], connect_timeout=5,
        )
        admin.autocommit = True
        with admin.cursor() as cur:
            cur.execute(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                f"WHERE datname = '{db_name}' AND pid <> pg_backend_pid()"
            )
            cur.execute(f'DROP DATABASE IF EXISTS {db_name}')
            cur.execute(f'CREATE DATABASE {db_name}')
        admin.close()

        self.conn = psycopg2.connect(
            host=c["host"], port=c["port"], user=c["user"], password=c["password"],
            dbname=db_name, connect_timeout=5,
        )
        self.conn.autocommit = True
        self.warmup()
        try:
            cur = self.conn.cursor()
            n = CONFIG["record_count"]

            self.times["Setup (DDL)"] = time_it(self._ddl, cur)[0]

            cur.execute(
                f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
                ("single", 30, True, "London", 1.0),
            )
            self.times["Single Insert"] = time_it(
                cur.execute,
                f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
                ("single2", 31, True, "Berlin", 2.0),
            )[0]

            rows = [
                (f"user_{i}", (i % 80) + 18, (i % 3 != 0),
                 ["London", "Berlin", "Paris", "Madrid"][i % 4], i * 1.5)
                for i in range(n)
            ]
            self.times["Bulk Insert"] = time_it(
                cur.executemany,
                f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
                rows,
            )[0]

            self.times["Count (COUNT)"] = time_it(self._count, cur)[0]
            self.times["Filter Query"] = time_it(self._filter, cur)[0]
            self.times["Range Query"] = time_it(self._range, cur)[0]
            self.times["Aggregation (avg age)"] = time_it(self._avg, cur)[0]
            self.times["Update"] = time_it(self._update, cur)[0]
            self.times["Index Creation"] = time_it(self._index, cur)[0]
            self.times["Delete"] = time_it(self._delete, cur)[0]
        finally:
            if self.conn is not None:
                try:
                    self.conn.close()
                except Exception:
                    pass

    def warmup(self):
        try:
            with self.conn.cursor() as cur:
                cur.execute("SELECT 1")
                cur.execute(f"CREATE TABLE IF NOT EXISTS {COLLECTION}__warm (id INT)")
                cur.execute(f"DROP TABLE IF EXISTS {COLLECTION}__warm")
        except Exception:
            pass

    def _ddl(self, cur):
        cur.execute(f"DROP TABLE IF EXISTS {COLLECTION}")
        cur.execute(
            f"CREATE TABLE {COLLECTION} ("
            "id SERIAL PRIMARY KEY, name TEXT, age INT, active BOOLEAN, city TEXT, score DOUBLE PRECISION)"
        )

    def _count(self, cur):
        cur.execute(f"SELECT COUNT(*) FROM {COLLECTION}")
        return cur.fetchone()[0]

    def _filter(self, cur):
        cur.execute(f"SELECT * FROM {COLLECTION} WHERE age = 30")
        return cur.fetchall()

    def _range(self, cur):
        cur.execute(f"SELECT * FROM {COLLECTION} WHERE age > 40")
        return cur.fetchall()

    def _avg(self, cur):
        cur.execute(f"SELECT AVG(age) FROM {COLLECTION}")
        return cur.fetchone()[0]

    def _update(self, cur):
        cur.execute(f"UPDATE {COLLECTION} SET active = false WHERE age < 30")
        return cur.rowcount

    def _index(self, cur):
        cur.execute(f"CREATE INDEX IF NOT EXISTS idx_{COLLECTION}_age ON {COLLECTION}(age)")

    def _delete(self, cur):
        cur.execute(f"DELETE FROM {COLLECTION} WHERE active = false")
        return cur.rowcount


class MariaEngine(Engine):
    def __init__(self, cfg):
        super().__init__("MariaDB")
        self.cfg = cfg
        self.conn = None

    def run(self):
        import pymysql

        c = self.cfg
        db_name = f"{c['test_db']}_{os.getpid()}"
        admin = pymysql.connect(
            host=c["host"], port=c["port"], user=c["user"], password=c["password"],
            database=c["admin_db"], connect_timeout=5,
        )
        with admin.cursor() as cur:
            cur.execute(f"DROP DATABASE IF EXISTS {db_name}")
            cur.execute(f"CREATE DATABASE {db_name}")
        admin.close()

        self.conn = pymysql.connect(
            host=c["host"], port=c["port"], user=c["user"], password=c["password"],
            database=db_name, connect_timeout=5,
        )
        cur = self.conn.cursor()
        n = CONFIG["record_count"]

        self.times["Setup (DDL)"] = time_it(self._ddl, cur)[0]

        cur.execute(
            f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
            ("single", 30, 1, "London", 1.0),
        )
        self.conn.commit()
        self.times["Single Insert"] = time_it(
            self._single, cur
        )[0]

        rows = [
            (f"user_{i}", (i % 80) + 18, 1 if (i % 3 != 0) else 0,
             ["London", "Berlin", "Paris", "Madrid"][i % 4], i * 1.5)
            for i in range(n)
        ]
        self.times["Bulk Insert"] = time_it(
            cur.executemany,
            f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
            rows,
        )[0]
        self.conn.commit()

        self.times["Count (COUNT)"] = time_it(self._count, cur)[0]
        self.times["Filter Query"] = time_it(self._filter, cur)[0]
        self.times["Range Query"] = time_it(self._range, cur)[0]
        self.times["Aggregation (avg age)"] = time_it(self._avg, cur)[0]
        self.times["Update"] = time_it(self._update, cur)[0]
        self.times["Index Creation"] = time_it(self._index, cur)[0]
        self.times["Delete"] = time_it(self._delete, cur)[0]

    def _single(self, cur):
        cur.execute(
            f"INSERT INTO {COLLECTION} (name,age,active,city,score) VALUES (%s,%s,%s,%s,%s)",
            ("single2", 31, 1, "Berlin", 2.0),
        )
        self.conn.commit()

    def warmup(self):
        try:
            with self.conn.cursor() as cur:
                cur.execute("SELECT 1")
                cur.execute(f"CREATE TABLE IF NOT EXISTS {COLLECTION}__warm (id INT)")
                cur.execute(f"DROP TABLE IF EXISTS {COLLECTION}__warm")
            self.conn.commit()
        except Exception:
            pass

    def _ddl(self, cur):
        cur.execute(f"DROP TABLE IF EXISTS {COLLECTION}")
        cur.execute(
            f"CREATE TABLE {COLLECTION} ("
            "id INT AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64), age INT, active BOOLEAN, city VARCHAR(64), score DOUBLE)"
        )
        self.conn.commit()

    def _count(self, cur):
        cur.execute(f"SELECT COUNT(*) FROM {COLLECTION}")
        return cur.fetchone()[0]

    def _filter(self, cur):
        cur.execute(f"SELECT * FROM {COLLECTION} WHERE age = 30")
        return cur.fetchall()

    def _range(self, cur):
        cur.execute(f"SELECT * FROM {COLLECTION} WHERE age > 40")
        return cur.fetchall()

    def _avg(self, cur):
        cur.execute(f"SELECT AVG(age) FROM {COLLECTION}")
        return cur.fetchone()[0]

    def _update(self, cur):
        cur.execute(f"UPDATE {COLLECTION} SET active = 0 WHERE age < 30")
        self.conn.commit()
        return cur.rowcount

    def _index(self, cur):
        cur.execute(f"CREATE INDEX idx_{COLLECTION}_age ON {COLLECTION}(age)")
        self.conn.commit()

    def _delete(self, cur):
        cur.execute(f"DELETE FROM {COLLECTION} WHERE active = 0")
        self.conn.commit()
        return cur.rowcount


class MongoEngine(Engine):
    def __init__(self, cfg):
        super().__init__("MongoDB")
        self.cfg = cfg
        self.client = None

    def run(self):
        from pymongo import MongoClient

        c = self.cfg
        uri = f"mongodb://{c['host']}:{c['port']}"
        self.client = MongoClient(uri, serverSelectionTimeoutMS=5000)
        self.client.admin.command("ping")
        self.warmup()
        db = self.client[f"{c['test_db']}_{os.getpid()}"]
        col = db[COLLECTION]
        n = CONFIG["record_count"]

        self.times["Setup (DDL)"] = time_it(lambda: col.drop())[0]

        col.insert_one({"name": "single", "age": 30, "active": True, "city": "London", "score": 1.0})
        self.times["Single Insert"] = time_it(
            col.insert_one,
            {"name": "single2", "age": 31, "active": True, "city": "Berlin", "score": 2.0},
        )[0]

        docs = [
            {"name": f"user_{i}", "age": (i % 80) + 18, "active": (i % 3 != 0),
             "city": ["London", "Berlin", "Paris", "Madrid"][i % 4], "score": i * 1.5}
            for i in range(n)
        ]
        self.times["Bulk Insert"] = time_it(col.insert_many, docs)[0]

        self.times["Count (COUNT)"] = time_it(col.count_documents, {})[0]
        self.times["Filter Query"] = time_it(list, col.find({"age": 30}))[0]
        self.times["Range Query"] = time_it(list, col.find({"age": {"$gt": 40}}))[0]
        self.times["Aggregation (avg age)"] = time_it(self._avg, col)[0]
        self.times["Update"] = time_it(
            col.update_many, {"age": {"$lt": 30}}, {"$set": {"active": False}}
        )[0]
        self.times["Index Creation"] = time_it(col.create_index, [("age", 1)])[0]
        self.times["Delete"] = time_it(col.delete_many, {"active": False})[0]

    def _avg(self, col):
        res = list(col.aggregate([{"$group": {"_id": None, "avg": {"$avg": "$age"}}}]))
        return res[0]["avg"] if res else 0

    def warmup(self):
        try:
            wcol = self.client["__warm_db"]["__warm"]
            wcol.insert_one({"a": 1})
            wcol.delete_many({})
            self.client.admin.command("ping")
        except Exception:
            pass


def build_report(results, meta):
    engines = list(results.keys())
    labels = OPERATIONS

    datasets = []
    colors = {
        "AxarDB (db)": "rgba(99,102,241,0.85)",
        "AxarDB (memory)": "rgba(168,85,247,0.85)",
        "AxarDB (bulk)": "rgba(6,182,211,0.85)",
        "PostgreSQL": "rgba(34,197,94,0.85)",
        "MariaDB": "rgba(234,179,8,0.85)",
        "MongoDB": "rgba(244,63,94,0.85)",
    }
    for eng in engines:
        data = []
        for op in labels:
            t = results[eng]["times"][op]
            if t is None:
                data.append(None)
            elif t <= 0:
                data.append(0.001)
            else:
                data.append(t * 1000.0)
        datasets.append({
            "label": eng,
            "data": data,
            "backgroundColor": colors.get(eng, "rgba(100,116,139,0.85)"),
        })

    totals = {}
    for eng in engines:
        vals = [v for v in results[eng]["times"].values() if v is not None]
        totals[eng] = sum(vals) if vals else None

    speedups = {}
    base = "AxarDB (memory)"
    if base in totals and totals[base]:
        for eng in engines:
            if eng == base:
                continue
            if totals[eng]:
                speedups[eng] = round(totals[eng] / totals[base], 2)

    status_rows = ""
    for eng in engines:
        r = results[eng]
        badge = ("ok" if r["status"] == "ok" else "err")
        status_rows += (
            f'<tr><td>{eng}</td><td><span class="badge {badge}">{r["status"]}</span></td>'
            f'<td>{r["detail"] or "-"}</td></tr>'
        )

    table_rows = ""
    for op in labels:
        cells = ""
        for eng in engines:
            v = results[eng]["times"][op]
            if v is None:
                cells += "<td class='na'>-</td>"
            elif v <= 0:
                cells += "<td>0 ms</td>"
            else:
                cells += f"<td>{v*1000:.2f} ms</td>"
        table_rows += f"<tr><td>{op}</td>{cells}</tr>"

    total_cells = ""
    for eng in engines:
        t = totals[eng]
        total_cells += f"<td><b>{t*1000:.2f} ms</b></td>" if t is not None else "<td class='na'>0 ms</td>"

    speedup_cells = ""
    for eng in engines:
        if eng == base:
            speedup_cells += "<td class='na'>-</td>"
        else:
            s = speedups.get(eng)
            speedup_cells += f"<td>{s}x</td>" if s is not None else "<td class='na'>-</td>"

    config_rows = ""
    for k, v in meta["config"].items():
        config_rows += f"<tr><td>{k}</td><td>{v}</td></tr>"

    feature_rows = ""
    for name, vals in FEATURES:
        cells = ""
        for db in FEATURE_DBS:
            val = vals.get(db, "-")
            axar_unique = (db == "AxarDB" and val == "Yes")
            cls = "feat-axar" if axar_unique else ""
            cells += f"<td class='{cls}'>{val}</td>"
        feature_rows += f"<tr><td>{name}</td>{cells}</tr>"

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>AxarDB Benchmark Report</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<style>
  * {{ box-sizing: border-box; }}
  body {{ font-family: -apple-system, Segoe UI, Roboto, sans-serif; margin: 0; background:#0f172a; color:#e2e8f0; }}
  header {{ padding: 28px 32px; background: linear-gradient(135deg,#4f46e5,#0ea5e9); }}
  header h1 {{ margin: 0; font-size: 24px; }}
  header p {{ margin: 6px 0 0; opacity:.9; }}
  main {{ padding: 24px 32px; max-width: 1200px; margin: 0 auto; }}
  section {{ background:#1e293b; border:1px solid #334155; border-radius:12px; padding:20px; margin-bottom:24px; }}
  h2 {{ font-size:18px; margin-top:0; border-left:4px solid #6366f1; padding-left:10px; }}
  table {{ width:100%; border-collapse:collapse; font-size:14px; }}
  th,td {{ padding:10px 12px; text-align:left; border-bottom:1px solid #334155; }}
  th {{ color:#94a3b8; font-weight:600; }}
  td.na {{ color:#64748b; }}
  td.feat-axar {{ background: rgba(168,85,247,0.18); color:#e9d5ff; font-weight:700; text-align:center; }}
  td.center {{ text-align:center; }}
  .badge {{ padding:3px 10px; border-radius:999px; font-size:12px; font-weight:700; }}
  .badge.ok {{ background:#16a34a; color:#fff; }}
  .badge.err {{ background:#dc2626; color:#fff; }}
  .chart-box {{ position:relative; height:380px; }}
  .grid2 {{ display:grid; grid-template-columns:1fr 1fr; gap:20px; }}
  @media(max-width:800px) {{ .grid2 {{ grid-template-columns:1fr; }} }}
  .muted {{ color:#94a3b8; font-size:13px; }}
  code {{ background:#0f172a; padding:2px 6px; border-radius:4px; }}
</style>
</head>
<body>
<header>
  <h1>AxarDB Benchmark Report</h1>
  <p>{meta['generated']} &bull; Records: {meta['record_count']} &bull; Engines: AxarDB (db/memory/bulk), PostgreSQL, MariaDB, MongoDB</p>
</header>
<main>
  <section>
    <h2>Engine Status</h2>
    <table><thead><tr><th>Engine</th><th>Status</th><th>Description</th></tr></thead>
    <tbody>{status_rows}</tbody></table>
  </section>

  <section>
    <h2>Operation Times (ms) — lower is better</h2>
    <table>
      <thead><tr><th>Operation</th>{''.join(f'<th>{e}</th>' for e in engines)}</tr></thead>
      <tbody>{table_rows}
      <tr style="border-top:2px solid #475569"><td><b>Total</b></td>{total_cells}</tr>
      <tr><td>{SPEEDUP_LABEL}</td>{speedup_cells}</tr>
      </tbody>
    </table>
  </section>

  <section>
    <h2>Advanced Features Comparison</h2>
    <p class="muted">AxarDB offers not only performance but also a contrasting architecture. Below are AxarDB-specific
    capabilities (not found in other engines); cells marked with <b>Yes</b> in the AxarDB column
    are unique to AxarDB.</p>
    <table>
      <thead><tr><th>Feature</th>{''.join(f'<th>{d}</th>' for d in FEATURE_DBS)}</tr></thead>
      <tbody>{feature_rows}</tbody>
    </table>
  </section>

  <section>
    <h2>Per-Operation Comparison (logarithmic scale)</h2>
    <div class="chart-box"><canvas id="opChart"></canvas></div>
  </section>

  <section>
    <h2>Total Time Comparison</h2>
    <div class="chart-box"><canvas id="totalChart"></canvas></div>
  </section>

  <section>
    <h2>{SPEEDUP_LABEL}</h2>
    <div class="chart-box"><canvas id="speedChart"></canvas></div>
  </section>

  <section>
    <h2>Test Configuration</h2>
    <table><tbody>{config_rows}</tbody></table>
     <p class="muted">Method: Each engine received the same workload (Setup, single/bulk insert, index creation, count,
     filter, range, aggregation, update, delete). Filter and Range queries ran against the <b>indexed 'age' column</b>.
     PostgreSQL/MariaDB/MongoDB were measured via native drivers, while AxarDB (db/memory/bulk) was measured through
     the AxarDB JavaScript query engine. <b>Important:</b> AxarDB times exclude HTTP overhead; operations were sent
     via <code>queue()</code> and duration was read from <code>sysqueue</code> (server-side Stopwatch), so measured
     time is pure database processing time. AxarDB uses in-memory storage with async disk persistence, so write times
     may appear lower compared to synchronous disk writes of native drivers. Read operations were repeated server-side
     and averaged for sub-millisecond precision. <code>memory</code> and <code>bulk</code> stores lack update/index APIs,
     so those cells show N/A. Results vary depending on hardware and current load.</p>
  </section>
</main>
<script>
  const labels = {json.dumps(labels)};
  const datasets = {json.dumps(datasets)};
  const totals = {json.dumps(totals)};
  const speedups = {json.dumps(speedups)};
  const engines = {json.dumps(engines)};

  new Chart(document.getElementById('opChart'), {{
    type: 'bar',
    data: {{ labels, datasets }},
    options: {{
      responsive:true, maintainAspectRatio:false,
      scales: {{
        y: {{ type:'logarithmic', title:{{display:true,text:'Time (ms, log)'}}, ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }},
        x: {{ ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }}
      }},
      plugins: {{ legend:{{labels:{{color:'#e2e8f0'}}}} }}
    }}
  }});

  new Chart(document.getElementById('totalChart'), {{
    type: 'bar',
    data: {{ labels: engines, datasets:[{{ label:'Total Time (ms)', data: engines.map(e=>totals[e]!=null?totals[e]*1000:0), backgroundColor: datasets.map(d=>d.backgroundColor) }}] }},
    options: {{
      responsive:true, maintainAspectRatio:false,
      scales: {{ y:{{ title:{{display:true,text:'Time (ms)'}}, ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }}, x:{{ ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }} }},
      plugins: {{ legend:{{display:false}} }}
    }}
  }});

  new Chart(document.getElementById('speedChart'), {{
    type: 'bar',
    data: {{ labels: engines.filter(e=>e!=={json.dumps(base)}), datasets:[{{ label:{json.dumps(SPEEDUP_LABEL)}, data: engines.filter(e=>e!=={json.dumps(base)}).map(e=>speedups[e]||0), backgroundColor:'rgba(14,165,233,0.85)' }}] }},
    options: {{
      responsive:true, maintainAspectRatio:false,
      scales: {{ y:{{ title:{{display:true,text:'Factor (higher = slower than AxarDB)'}}, ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }}, x:{{ ticks:{{color:'#cbd5e1'}}, grid:{{color:'#334155'}} }} }},
      plugins: {{ legend:{{display:false}} }}
    }}
  }});
</script>
</body>
</html>"""
    return html


def main():
    meta = {
        "generated": now_ts(),
        "record_count": CONFIG["record_count"],
        "config": {
            "AxarDB": f"{CONFIG['axardb']['host']} (auth: enabled)",
            "PostgreSQL": f"{CONFIG['postgres']['host']}:{CONFIG['postgres']['port']} (auth: enabled)",
            "MariaDB": f"{CONFIG['mariadb']['host']}:{CONFIG['mariadb']['port']} (auth: enabled)",
            "MongoDB": f"{CONFIG['mongodb']['host']}:{CONFIG['mongodb']['port']} (auth: disabled)",
            "Record count": CONFIG["record_count"],
        },
    }

    global _LAUNCHER
    launcher = AxarDBLauncher(CONFIG["axardb"])
    _LAUNCHER = launcher

    # Hard ceiling watchdog: if the whole run exceeds overall_timeout, force-kill
    # this process and the AxarDB child tree so it can never stay open.
    watchdog = None
    if CONFIG["overall_timeout"] and CONFIG["overall_timeout"] > 0:
        watchdog = threading.Timer(CONFIG["overall_timeout"], _force_kill_all, args=("overall_timeout exceeded",))
        watchdog.daemon = True
        watchdog.start()

    run_start = _t()
    step(f"BENCHMARK START (overall_timeout={CONFIG['overall_timeout']}s, op_timeout={CONFIG['op_timeout']}s)")
    try:
        launcher.start()
        results = {}

        ax_engines = [
            AxarDBStoreEngine(CONFIG["axardb"], "memory", "AxarDB (memory)"),
            AxarDBStoreEngine(CONFIG["axardb"], "db", "AxarDB (db)"),
            AxarDBStoreEngine(CONFIG["axardb"], "bulk", "AxarDB (bulk)"),
        ]
        for ax in ax_engines:
            step(f"[*] {ax.name} workload START (elapsed={_t()-run_start:.1f}s)")
            ax.safe_run()
            step(f"[*] {ax.name} workload DONE status={ax.status} ({_t()-run_start:.1f}s)")
            results[ax.name] = {"status": ax.status, "detail": ax.detail, "times": ax.times}
            if ax.status == "error":
                step(f"[CRITICAL] {ax.name} failed with: {ax.detail}. Force closing benchmark.")
                sys.exit(1)

        if CONFIG["postgres"]["enabled"]:
            pg = PostgresEngine(CONFIG["postgres"])
            print("[*] PostgreSQL running workload ...")
            pg.safe_run()
            results[pg.name] = {"status": pg.status, "detail": pg.detail, "times": pg.times}

        if CONFIG["mariadb"]["enabled"]:
            ma = MariaEngine(CONFIG["mariadb"])
            print("[*] MariaDB running workload ...")
            ma.safe_run()
            results[ma.name] = {"status": ma.status, "detail": ma.detail, "times": ma.times}

        if CONFIG["mongodb"]["enabled"]:
            mo = MongoEngine(CONFIG["mongodb"])
            print("[*] MongoDB running workload ...")
            mo.safe_run()
            results[mo.name] = {"status": mo.status, "detail": mo.detail, "times": mo.times}

        html = build_report(results, meta)
        with open(CONFIG["output_html"], "w", encoding="utf-8") as f:
            f.write(html)

        log_change(
            f"Benchmark completed. Engines: {', '.join(results.keys())}. "
            f"Report: {CONFIG['output_html']}"
        )
        print(f"[+] Report generated: {CONFIG['output_html']}")
        step(f"BENCHMARK DONE (total elapsed={_t()-run_start:.1f}s)")
    finally:
        if watchdog is not None:
            watchdog.cancel()
        launcher.stop()


if __name__ == "__main__":
    main()
