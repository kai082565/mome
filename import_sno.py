"""
將舊系統 LAMP.DB 的燈號(Sno) 匯入新系統 SQLite 的 LampOrders.OrderNumber

匹配邏輯：
  LampType（燈種名稱）+ CusNo（客戶編號）+ Date0（起始日）
"""
import struct, os, sqlite3

BACKUP_DIR = r"C:\Users\USER\Desktop\LightData 2026-02-12 15;20;06 (完整備份)\bak1150114"
LAMP_DB    = os.path.join(BACKUP_DIR, "LAMP.DB")
SQLITE_DB  = r"C:\Users\USER\AppData\Roaming\TempleLampSystem\TempleLamp.db"

# ── Paradox reader ─────────────────────────────────────────────
def read_paradox_header(data):
    record_size = struct.unpack_from('<H', data, 0)[0]
    header_size = struct.unpack_from('<H', data, 2)[0]
    num_records = struct.unpack_from('<I', data, 6)[0]
    num_fields  = struct.unpack_from('<H', data, 0x21)[0]
    fdefs = [(data[0x78+i*2], data[0x78+i*2+1]) for i in range(num_fields)]
    fnames, pos = [], 0x78 + num_fields * 2
    while pos < header_size and len(fnames) < num_fields:
        if data[pos] >= 0x20:
            ts, tp, ok = [], pos, True
            for _ in range(num_fields):
                try: e = data.index(0, tp)
                except ValueError: ok = False; break
                r = data[tp:e]
                if not all(0x20 <= b < 0x7F for b in r) or not r: ok=False; break
                ts.append(r.decode('ascii')); tp = e+1
            if ok and len(ts) == num_fields: fnames = ts; break
        pos += 1
    return record_size, header_size, num_records, num_fields, fdefs, fnames

def decode_field(raw, ftype):
    if ftype == 0x01:
        return raw.rstrip(b'\x00').decode('cp950', errors='replace').strip()
    elif ftype in (0x04, 0x02):
        v = struct.unpack('>I', raw)[0]
        return (v ^ 0x80000000) if v & 0x80000000 else -(v ^ 0x7FFFFFFF)
    elif ftype == 0x06:
        ba = bytearray(raw)
        if ba[0] & 0x80: ba[0] ^= 0x80; v = struct.unpack('>d', bytes(ba))[0]
        else: ba = bytearray(b^0xFF for b in ba); v = -struct.unpack('>d', bytes(ba))[0]
        return int(v) if v == int(v) else v
    return raw.rstrip(b'\x00').decode('cp950', errors='replace').strip()

def read_records(fpath):
    with open(fpath, 'rb') as f: data = f.read()
    rs, hs, nr, nf, fdefs, fnames = read_paradox_header(data)
    blk = data[0x05] * 0x400
    recs, off = [], hs
    while off < len(data) and len(recs) < nr:
        add = struct.unpack_from('<h', data, off+4)[0]
        cnt = (add // rs) + 1 if add >= 0 else 0
        for i in range(cnt):
            if len(recs) >= nr: break
            ro = off + 6 + i * rs
            if ro + rs > len(data): break
            rec = data[ro:ro+rs]
            row, fo = {}, 0
            for n, (ft, fs) in zip(fnames, fdefs):
                row[n] = decode_field(rec[fo:fo+fs], ft); fo += fs
            recs.append(row)
        off += blk
    return fnames, recs

# ── 轉換舊系統日期 110/01/01 → 公元年 2021-01-01 ──────────────
def roc_to_gregorian(roc_str):
    """110/01/01 → 2021-01-01"""
    try:
        parts = str(roc_str).strip().split('/')
        if len(parts) != 3: return None
        y, m, d = int(parts[0]), int(parts[1]), int(parts[2])
        return f"{y+1911}-{m:02d}-{d:02d}"
    except: return None

# ── 主流程 ─────────────────────────────────────────────────────
print("讀取舊系統 LAMP.DB...")
_, old_recs = read_records(LAMP_DB)
print(f"  共 {len(old_recs)} 筆")

# 建立查找 key: (LampName, CusNo, StartDate) → Sno
old_map = {}
skip = 0
for r in old_recs:
    sno = r.get('Sno', 0)
    if not isinstance(sno, int) or sno <= 0:
        skip += 1; continue
    lamp = r.get('LampType', '').strip()
    cus  = str(r.get('CusNo', '')).strip().lstrip('0')  # 去前導零
    date = roc_to_gregorian(r.get('Date0', ''))
    if not lamp or not cus or not date: continue
    key = (lamp, cus, date)
    if key not in old_map:
        old_map[key] = str(sno)

print(f"  有效燈號 {len(old_map)} 筆（跳過 {skip} 筆）")

# 連接新系統 SQLite
print(f"\n連接新系統 SQLite: {SQLITE_DB}")
conn = sqlite3.connect(SQLITE_DB)
cur = conn.cursor()

# 讀取新系統燈種對應（LampName → LampId）
cur.execute("SELECT Id, LampName FROM Lamps")
lamp_map = {name: lid for lid, name in cur.fetchall()}
print(f"  燈種對應：{lamp_map}")

# 讀取新系統客戶對應（CustomerCode 去前導零 → CustomerId）
cur.execute("SELECT Id, CustomerCode FROM Customers WHERE CustomerCode IS NOT NULL")
cus_map = {str(code).lstrip('0'): cid for cid, code in cur.fetchall() if code}

# 讀取新系統所有訂單
cur.execute("SELECT Id, CustomerId, LampId, StartDate, OrderNumber FROM LampOrders")
orders = cur.fetchall()
print(f"  新系統訂單：{len(orders)} 筆")

updated = 0
not_found = 0

for order_id, customer_id, lamp_id, start_date_raw, existing_order_num in orders:
    # 取得燈種名稱
    lamp_name = next((n for n, lid in lamp_map.items() if lid == lamp_id), None)
    if not lamp_name: continue

    # 取得客戶編號
    cus_code = cus_map.get(customer_id, None)
    # customer_id 是 GUID，反查需要用 id
    # 重新查
    cur.execute("SELECT CustomerCode FROM Customers WHERE Id=?", (customer_id,))
    row = cur.fetchone()
    cus_code = str(row[0]).lstrip('0') if row and row[0] else None
    if not cus_code: continue

    # 日期：只取 YYYY-MM-DD
    start_date = str(start_date_raw)[:10] if start_date_raw else None
    if not start_date: continue

    key = (lamp_name, cus_code, start_date)
    sno = old_map.get(key)

    if sno:
        cur.execute("UPDATE LampOrders SET OrderNumber=? WHERE Id=?", (sno, order_id))
        updated += 1
    else:
        not_found += 1

conn.commit()
conn.close()

print(f"\n完成！")
print(f"  成功匯入燈號：{updated} 筆")
print(f"  找不到對應：{not_found} 筆（可能是新系統才有的訂單，燈號留空）")
