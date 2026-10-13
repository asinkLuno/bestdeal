"""Dump interesting types from a .NET interop assembly (read-only)."""
import sys
import dnfile

DLL = sys.argv[1]
PATTERN = sys.argv[2].lower() if len(sys.argv) > 2 else None

pe = dnfile.dnPE(DLL)


def s(name):
    if name is None:
        return ""
    if isinstance(name, bytes):
        return name.decode("utf-8", "replace")
    return str(name)


types = pe.net.mdtables.TypeDef
for i, row in enumerate(types):
    name = s(row.TypeName)
    ns = s(row.TypeNamespace)
    full = f"{ns}.{name}" if ns else name
    if PATTERN and PATTERN not in full.lower():
        continue
    fields = []
    for idx in row.FieldList:
        fr = getattr(idx, "row", None)
        if fr is not None:
            fields.append(s(fr.Name))
    methods = []
    for idx in row.MethodList:
        mr = getattr(idx, "row", None)
        if mr is not None:
            methods.append(s(mr.Name))
    # dedupe, keep order
    seen = set()
    methods = [m for m in methods if not (m in seen or seen.add(m))]
    print(f"=== {full}")
    if fields:
        print("  fields: " + ", ".join(fields[:40]))
    print("  methods: " + ", ".join(methods[:60]))
