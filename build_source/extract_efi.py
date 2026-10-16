import struct, sys, os

def read_esp_offset(img_path):
    with open(img_path, 'rb') as f:
        f.seek(0)
        mbr = f.read(512)
        # MBR 分区表 (签名 55AA)
        if mbr[0x1FE:0x200] == b'\x55\xaa':
            for i in range(4):
                entry = mbr[0x1BE + i*16 : 0x1BE + (i+1)*16]
                ptype = entry[4]
                start_lba = struct.unpack('<I', entry[8:12])[0]
                if ptype == 0xef and start_lba > 0:
                    return start_lba * 512
        # GPT fallback
        f.seek(512)
        gpt_header = f.read(512)
        if gpt_header[0:8] == b'EFI PART':
            entries_lba = struct.unpack('<Q', gpt_header[72:80])[0]
            num_entries = struct.unpack('<I', gpt_header[80:84])[0]
            entry_size = struct.unpack('<I', gpt_header[84:88])[0]
            esp_guid = bytes.fromhex('28732AC11FF8D211BA4B00A0C93EC93B')
            f.seek(entries_lba * 512)
            for i in range(num_entries):
                entry = f.read(entry_size)
                if len(entry) < 128:
                    break
                if entry[0:16] == esp_guid:
                    start_lba = struct.unpack('<Q', entry[32:40])[0]
                    return start_lba * 512
    raise Exception('未找到 ESP 分区')

def parse_fat32(img_path, part_offset):
    with open(img_path, 'rb') as f:
        f.seek(part_offset)
        bpb = f.read(512)
        bps = struct.unpack('<H', bpb[11:13])[0]
        spc = bpb[13]
        rsvd = struct.unpack('<H', bpb[14:16])[0]
        nfat = bpb[16]
        fatsz = struct.unpack('<I', bpb[36:40])[0]
        root_cluster = struct.unpack('<I', bpb[44:48])[0]
        fat_off = part_offset + rsvd * bps
        data_off = fat_off + nfat * fatsz * bps
        csize = spc * bps

        def read_cluster(c):
            with open(img_path, 'rb') as f:
                f.seek(data_off + (c - 2) * csize)
                return f.read(csize)

        def fat_entry(c):
            with open(img_path, 'rb') as f:
                f.seek(fat_off + c * 4)
                return struct.unpack('<I', f.read(4))[0] & 0x0FFFFFFF

        def chain(start):
            data = b''
            c = start
            while 2 <= c < 0x0FFFFFF8:
                data += read_cluster(c)
                c = fat_entry(c)
            return data

        def find_entry(data, target):
            for i in range(0, len(data), 32):
                e = data[i:i+32]
                if len(e) < 32 or e[0] == 0x00:
                    break
                if e[0] == 0xE5:
                    continue
                base = e[0:8].rstrip(b' ').decode('ascii', 'ignore')
                ext = e[8:11].rstrip(b' ').decode('ascii', 'ignore')
                full = (base + ('.' + ext if ext else '')).upper()
                first = (struct.unpack('<H', e[20:22])[0] << 16) + struct.unpack('<H', e[26:28])[0]
                size = struct.unpack('<I', e[28:32])[0]
                if full == target.upper():
                    return first, size
            return None, None

        return {
            'chain': chain,
            'find_entry': find_entry,
            'root_cluster': root_cluster,
        }

def extract_file(img_path, target_path):
    part_off = read_esp_offset(img_path)
    fat = parse_fat32(img_path, part_off)
    chain = fat['chain']
    find = fat['find_entry']

    root = chain(fat['root_cluster'])
    efi_cluster, _ = find(root, 'EFI')
    efi = chain(efi_cluster)
    boot_cluster, _ = find(efi, 'BOOT')
    boot = chain(boot_cluster)
    file_cluster, size = find(boot, target_path)
    if file_cluster is None:
        raise Exception('未找到 ' + target_path)
    data = chain(file_cluster)[:size]
    return data

if __name__ == '__main__':
    img = sys.argv[1]
    out = sys.argv[2]
    data = extract_file(img, 'BOOTX64.EFI')
    with open(out, 'wb') as f:
        f.write(data)
    print('提取成功:', out, len(data), 'bytes')
