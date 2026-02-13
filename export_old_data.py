"""
Export Paradox .DB files (CUS.DB, LAMP.DB) to CSV.

Reads binary Paradox 7 format files from the old LightData backup
and outputs UTF-8 CSV files.
"""

import struct
import csv
import os
import sys

BACKUP_DIR = r"C:\Users\USER\Desktop\LightData 2026-02-12 15;20;06 (完整備份)"
OUTPUT_DIR = r"C:\Users\USER\Desktop\點燈"


def read_paradox_header(data):
    """Parse Paradox .DB file header and return metadata."""
    record_size = struct.unpack_from('<H', data, 0x0000)[0]
    header_size = struct.unpack_from('<H', data, 0x0002)[0]
    num_records = struct.unpack_from('<I', data, 0x0006)[0]
    num_fields = struct.unpack_from('<H', data, 0x0021)[0]

    # Field definitions start at offset 0x78
    # Each field: type (1 byte) + size (1 byte)
    field_defs = []
    for i in range(num_fields):
        ftype = data[0x78 + i * 2]
        fsize = data[0x78 + i * 2 + 1]
        field_defs.append((ftype, fsize))

    # Field names are null-terminated ASCII strings in the header.
    # Search for them by looking for consecutive null-terminated ASCII strings.
    names_region_start = 0x78 + num_fields * 2
    field_names = []
    pos = names_region_start
    while pos < header_size and len(field_names) == 0:
        if data[pos] >= 0x20 and data[pos] < 0x7F:
            name_start = pos
            test_names = []
            tpos = name_start
            valid = True
            for _ in range(num_fields):
                try:
                    end = data.index(0x00, tpos)
                except ValueError:
                    valid = False
                    break
                raw_name = data[tpos:end]
                # Field names should be pure ASCII
                if not all(0x20 <= b < 0x7F for b in raw_name) or len(raw_name) == 0:
                    valid = False
                    break
                test_names.append(raw_name.decode('ascii'))
                tpos = end + 1
            if valid and len(test_names) == num_fields:
                field_names = test_names
                break
        pos += 1

    if not field_names:
        field_names = [f"Field{i}" for i in range(num_fields)]

    return {
        'record_size': record_size,
        'header_size': header_size,
        'num_records': num_records,
        'num_fields': num_fields,
        'field_defs': field_defs,
        'field_names': field_names,
    }


def decode_alpha_field(raw_bytes):
    """Decode a Paradox Alpha (text) field from cp950/Big5 to string."""
    # Strip trailing null bytes first
    raw = raw_bytes.rstrip(b'\x00')
    if not raw:
        return ''
    try:
        return raw.decode('cp950').strip()
    except UnicodeDecodeError:
        try:
            return raw.decode('big5', errors='replace').strip()
        except Exception:
            return raw.decode('latin-1', errors='replace').strip()


def decode_short_field(raw_bytes):
    """Decode a Paradox Short integer (type 0x03), 2 bytes big-endian with high bit toggle."""
    if raw_bytes == b'\x00\x00':
        return 0
    val = struct.unpack('>H', raw_bytes)[0]
    # If high bit is set, number is positive (toggle it off)
    if val & 0x8000:
        return val ^ 0x8000
    else:
        # Negative: invert all bits, negate
        return -(val ^ 0x7FFF)


def decode_long_field(raw_bytes):
    """Decode a Paradox Long integer (type 0x04), 4 bytes big-endian with high bit toggle."""
    if raw_bytes == b'\x00\x00\x00\x00':
        return 0
    val = struct.unpack('>I', raw_bytes)[0]
    if val & 0x80000000:
        return val ^ 0x80000000
    else:
        return -(val ^ 0x7FFFFFFF)


def decode_number_field(raw_bytes):
    """Decode a Paradox Number (double) field (type 0x06), 8 bytes."""
    if raw_bytes == b'\x00' * 8:
        return 0.0
    ba = bytearray(raw_bytes)
    if ba[0] & 0x80:
        # Positive: toggle high bit
        ba[0] ^= 0x80
        return struct.unpack('>d', bytes(ba))[0]
    else:
        # Negative: invert all bits
        ba = bytearray(b ^ 0xFF for b in ba)
        return -struct.unpack('>d', bytes(ba))[0]


def decode_field(raw_bytes, ftype):
    """Decode a field based on its Paradox type code."""
    if ftype == 0x01:  # Alpha
        return decode_alpha_field(raw_bytes)
    elif ftype == 0x03:  # Short
        return decode_short_field(raw_bytes)
    elif ftype == 0x04:  # Long integer
        return decode_long_field(raw_bytes)
    elif ftype == 0x06:  # Number (double)
        val = decode_number_field(raw_bytes)
        # Format as integer if it's a whole number
        if val == int(val):
            return int(val)
        return val
    elif ftype == 0x02:  # Date
        return decode_long_field(raw_bytes)
    else:
        # Unknown type: try alpha decode
        return decode_alpha_field(raw_bytes)


def read_paradox_records(filepath):
    """Read all records from a Paradox .DB file."""
    with open(filepath, 'rb') as f:
        data = f.read()

    hdr = read_paradox_header(data)
    print(f"  File: {os.path.basename(filepath)}")
    print(f"  Record size: {hdr['record_size']}, Header size: {hdr['header_size']}")
    print(f"  Num records: {hdr['num_records']}, Num fields: {hdr['num_fields']}")
    for fname, (ftype, fsize) in zip(hdr['field_names'], hdr['field_defs']):
        print(f"    {fname}: type={ftype:#04x} size={fsize}")

    record_size = hdr['record_size']
    header_size = hdr['header_size']
    field_defs = hdr['field_defs']
    field_names = hdr['field_names']
    total_records = hdr['num_records']

    # Data is stored in blocks after the header.
    # Each block has a 6-byte header: next_block(2), prev_block(2), addDataSize(2)
    # The block payload size can be computed from addDataSize.
    # Block size in bytes = header_size (which is also the block data area size).
    # Actually the block size is record_size * records_per_block + 6.
    # Let's compute: the "max table size" byte at offset 0x05 gives block size in KB.
    max_table_size_kb = data[0x05]  # in units of 1KB
    block_data_size = max_table_size_kb * 0x400  # total block size in bytes
    block_header_size = 6
    max_records_per_block = (block_data_size - block_header_size) // record_size

    records = []
    block_offset = header_size
    file_size = len(data)

    while block_offset < file_size and len(records) < total_records:
        if block_offset + block_header_size > file_size:
            break

        next_block = struct.unpack_from('<H', data, block_offset)[0]
        prev_block = struct.unpack_from('<H', data, block_offset + 2)[0]
        add_data_size = struct.unpack_from('<h', data, block_offset + 4)[0]

        # Number of records in this block:
        # addDataSize indicates the used data size in the block (excluding header)
        # but its meaning can vary. Often: num_records_in_block = (addDataSize // record_size) + 1
        # Or: used_bytes = addDataSize, records = used_bytes // record_size
        # Let's use: records_in_block = (addDataSize + record_size) // record_size
        # Actually addDataSize = (num_records - 1) * record_size for the "extra" bytes beyond first record
        if add_data_size >= 0:
            records_in_block = (add_data_size // record_size) + 1
        else:
            records_in_block = 0

        if records_in_block > max_records_per_block:
            records_in_block = max_records_per_block

        data_start = block_offset + block_header_size
        for i in range(records_in_block):
            if len(records) >= total_records:
                break
            rec_offset = data_start + i * record_size
            if rec_offset + record_size > file_size:
                break

            rec_data = data[rec_offset:rec_offset + record_size]

            # Parse fields
            row = {}
            field_offset = 0
            for fname, (ftype, fsize) in zip(field_names, field_defs):
                raw = rec_data[field_offset:field_offset + fsize]
                row[fname] = decode_field(raw, ftype)
                field_offset += fsize

            records.append(row)

        # Move to next block
        block_offset += block_data_size

    return field_names, records


def export_cus():
    """Export CUS.DB to customers.csv."""
    filepath = os.path.join(BACKUP_DIR, "CUS.DB")
    print(f"\nProcessing CUS.DB...")
    field_names, records = read_paradox_records(filepath)

    # Output columns: CusNo,CusName,CusTel,CusAddr,CusZone,YY,MM,DD,HH,Zip,Mark
    out_columns = ['CusNo', 'CusName', 'CusTel', 'CusAddr', 'CusZone', 'YY', 'MM', 'DD', 'HH', 'Zip', 'Mark']
    outpath = os.path.join(OUTPUT_DIR, "customers.csv")

    with open(outpath, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=out_columns, extrasaction='ignore')
        writer.writeheader()
        for rec in records:
            writer.writerow({col: rec.get(col, '') for col in out_columns})

    print(f"  Exported {len(records)} records to {outpath}")
    return len(records)


def export_lamp():
    """Export LAMP.DB to lamporders.csv."""
    filepath = os.path.join(BACKUP_DIR, "LAMP.DB")
    print(f"\nProcessing LAMP.DB...")
    field_names, records = read_paradox_records(filepath)

    # Output columns: LampType,Date0,CusNo,Fare,Date1,GodType,TempleType,Mark
    out_columns = ['LampType', 'Date0', 'CusNo', 'Fare', 'Date1', 'GodType', 'TempleType', 'Mark']
    outpath = os.path.join(OUTPUT_DIR, "lamporders.csv")

    with open(outpath, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=out_columns, extrasaction='ignore')
        writer.writeheader()
        for rec in records:
            writer.writerow({col: rec.get(col, '') for col in out_columns})

    print(f"  Exported {len(records)} records to {outpath}")
    return len(records)


def main():
    print("=" * 60)
    print("Paradox .DB to CSV Exporter")
    print("=" * 60)

    cus_count = export_cus()
    lamp_count = export_lamp()

    print("\n" + "=" * 60)
    print(f"DONE. Customers: {cus_count}, Lamp Orders: {lamp_count}")
    print("=" * 60)


if __name__ == '__main__':
    main()
