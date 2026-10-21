import io, re

p = r'D:\app\build_source\src\NativeCore.cs'
s = io.open(p, encoding='utf-8').read()

if 'CreateNoWindow' in s:
    print('already patched')
else:
    lines = s.split('\n')
    out = []
    n = 0
    for ln in lines:
        out.append(ln)
        st = ln.strip()
        target = ('new ProcessStartInfo("cmd.exe"' in st or
                  'new ProcessStartInfo("power' + 'shell.exe"' in st)
        if target and ';' in st:
            mm = re.search(r'(var|ProcessStartInfo)\s+(\w+)\s*=', st)
            if mm:
                v = mm.group(2)
                indent = ln[:len(ln) - len(ln.lstrip())]
                out.append(indent + v + '.UseShellExecute = false;')
                out.append(indent + v + '.CreateNoWindow = true;')
                n += 1
    s = '\n'.join(out)
    io.open(p, 'w', encoding='utf-8').write(s)
    print('patched sites:', n)
