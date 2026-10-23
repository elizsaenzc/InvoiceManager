"""Auditoría funcional contra el contenedor en marcha.

Recorre todas las páginas (cada factura, su versión imprimible y cada cliente), ejecuta los
flujos completos de una factura (borrador → edición → emisión → cobro → anulación) con el
token antiforgery real, y comprueba validaciones, numeración correlativa y exportaciones.
Sale con código 1 si algo falla.
"""
import csv
import datetime as dt
import http.cookiejar
import io
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("BASE_URL", "http://localhost:8080")
failures: list[str] = []
passed = 0


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), NoRedirect)


def request(method, path, data=None):
    body = urllib.parse.urlencode(data, doseq=True).encode() if data is not None else None
    req = urllib.request.Request(BASE + path, data=body, method=method)
    if body is not None:
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
    try:
        with opener.open(req, timeout=30) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)


def get(path):
    return request("GET", path)


def text(raw):
    return raw.decode("utf-8", "replace")


def check(ok, label, detail=""):
    global passed
    if ok:
        passed += 1
        print(f"OK   {label}")
    else:
        failures.append(f"{label} {detail}".strip())
        print(f"FAIL {label} {detail}")


def token(path):
    status, raw, _ = get(path)
    m = re.search(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"', text(raw))
    if not m:
        raise SystemExit(f"Sin token antiforgery en {path} (HTTP {status})")
    return m.group(1)


def post(form_path, action, fields):
    data = {"__RequestVerificationToken": token(form_path), **fields}
    return request("POST", action, data)


def location(headers):
    return headers.get("Location") or headers.get("location") or ""


def ids(html, prefix):
    return sorted({int(x) for x in re.findall(rf'{prefix}/(\d+)', html)})


today = dt.date.today()

# ── Páginas principales ─────────────────────────────────────────────────────
for path, must in [
    ("/", "Facturación y cobros"), ("/Invoices", "Facturas"), ("/Invoices?status=Overdue", "Vencidas"),
    ("/Invoices?status=Draft", "Borradores"), ("/Invoices?status=Open", "Por cobrar"), ("/Invoices?status=Paid", "Cobradas"),
    ("/Invoices?q=seo", "Posicionamiento"), ("/Invoices/Create", "Nueva factura"), ("/Clients", "Clientes"),
    ("/Clients?sort=pendiente", "Pendiente"), ("/Clients/Create", "Nuevo cliente"), ("/Reports", "IVA trimestral"),
]:
    s, raw, _ = get(path)
    check(s == 200 and must in text(raw), f"GET {path}", f"HTTP {s}")

s, raw, _ = get("/Invoices/Details/999999")
check(s == 404 and "No hemos encontrado" in text(raw), "404 con página propia", f"HTTP {s}")

# ── Datos de demo coherentes ────────────────────────────────────────────────
_, raw, _ = get("/Invoices")
html = text(raw)
invoice_ids = ids(html, "/Invoices/Details")
check(len(invoice_ids) >= 80, f"facturas de demo ({len(invoice_ids)})")
for st in ["pill-paid", "pill-overdue", "pill-pending", "pill-draft", "pill-cancelled"]:
    check(st in html, f"hay facturas en estado {st[5:]}")

_, raw, _ = get("/")
dash = text(raw)
kpi = re.search(r"Facturado · 12 meses</div>\s*<div class=\"kpi-value\">([^<]+)</div>", dash)
check(kpi is not None and kpi.group(1).strip() not in ("0 €", ""), "panel con facturación real", kpi.group(1) if kpi else "sin KPI")
check(dash.count("/Invoices/Details/") >= 8, "panel con listas de facturas")

# Cada factura, su versión imprimible y su página de edición cuando procede
errors = 0
for i in invoice_ids:
    s, raw, _ = get(f"/Invoices/Details/{i}")
    page = text(raw)
    if s != 200:
        errors += 1
        print(f"     detalle {i}: HTTP {s}")
        continue
    if "Borrador de factura" not in page:
        s2, _, _ = get(f"/Invoices/Print/{i}")
        if s2 != 200:
            errors += 1
            print(f"     impresión {i}: HTTP {s2}")
check(errors == 0, f"detalle e impresión de {len(invoice_ids)} facturas", f"{errors} errores")

_, raw, _ = get("/Clients")
client_ids = ids(text(raw), "/Clients/Details")
check(len(client_ids) >= 14, f"clientes de demo ({len(client_ids)})")
bad = [c for c in client_ids if get(f"/Clients/Details/{c}")[0] != 200 or get(f"/Clients/Edit/{c}")[0] != 200]
check(not bad, "ficha y edición de todos los clientes", str(bad))

# ── Exportaciones y numeración ──────────────────────────────────────────────
s, raw, h = get("/Invoices/Export")
check(s == 200 and raw.startswith(b"\xef\xbb\xbf") and "text/csv" in h.get("Content-Type", ""), "CSV de facturas con BOM")
rows = list(csv.reader(io.StringIO(raw.decode("utf-8-sig")), delimiter=";"))
check(rows[0][:3] == ["Número", "Fecha", "Vencimiento"], "cabecera del CSV")
check(all(re.fullmatch(r"-?\d+,\d{2}", r[8]) for r in rows[1:]), "importes con coma decimal")


def cif_ok(v):
    if not re.fullmatch(r"[ABCDEFGHJNPQRSUVW]\d{7}[0-9A-J]", v or ""):
        return False
    s = 0
    for i, ch in enumerate(v[1:8]):
        n = int(ch)
        if i % 2 == 0:
            n *= 2
            s += n // 10 + n % 10
        else:
            s += n
    c = (10 - s % 10) % 10
    return v[8] in (str(c), "JABCDEFGHI"[c])


nifs = {r[4] for r in rows[1:] if r[4]}
check(nifs and all(cif_ok(n) for n in nifs), f"CIF de clientes válidos ({len(nifs)})", str([n for n in nifs if not cif_ok(n)]))

numbers = [r[0] for r in rows[1:] if r[0].startswith("FAC-")]
check(len(numbers) == len(set(numbers)), "números de factura únicos")
by_year: dict[str, list[int]] = {}
for n in numbers:
    _, y, seq = n.split("-")
    by_year.setdefault(y, []).append(int(seq))
gaps = {y: sorted(set(range(1, max(v) + 1)) - set(v)) for y, v in by_year.items()}
check(all(not g for g in gaps.values()), "serie correlativa sin huecos", str(gaps))

s, raw, _ = get("/Reports/Export")
check(s == 200 and raw.startswith(b"\xef\xbb\xbf"), "libro de facturas emitidas en CSV")

# ── Clientes: validación de NIF y alta ──────────────────────────────────────
base_client = {"Name": "Cliente Auditoría S.L.", "Email": "auditoria@example.com", "City": "27001 Lugo", "PaymentTermsDays": "30"}
s, raw, _ = post("/Clients/Create", "/Clients/Create", {**base_client, "TaxId": "B12345678"})
check(s == 200 and "NIF/CIF no válido" in text(raw), "rechaza CIF con control incorrecto", f"HTTP {s}")
s, raw, h = post("/Clients/Create", "/Clients/Create", {**base_client, "TaxId": "B27415637"})
check(s == 200 and "Ya existe un cliente" in text(raw), "rechaza CIF duplicado", f"HTTP {s}")
s, _, h = post("/Clients/Create", "/Clients/Create", {**base_client, "TaxId": "b-12345674"})
new_client = int(re.search(r"/Clients/Details/(\d+)", location(h)).group(1)) if s == 302 else 0
check(s == 302 and new_client > 0, "alta de cliente", f"HTTP {s}")
check("B12345674" in text(get(f"/Clients/Details/{new_client}")[1]), "NIF normalizado en mayúsculas")

# ── Flujo completo de una factura ───────────────────────────────────────────
iso = today.isoformat()
lines = {
    "Lines[0].Description": "Auditoría SEO técnica", "Lines[0].Quantity": "1", "Lines[0].UnitPrice": "450",
    "Lines[1].Description": "", "Lines[1].Quantity": "1", "Lines[1].UnitPrice": "",
    "Lines[2].Description": "Horas de soporte", "Lines[2].Quantity": "2.5", "Lines[2].UnitPrice": "45",
}
draft = {"ClientId": str(new_client), "IssueDate": iso, "DueDate": (today + dt.timedelta(days=30)).isoformat(),
         "TaxRate": "21", "Notes": "Pedido 2231", **lines}

s, raw, _ = post("/Invoices/Create", "/Invoices/Create", {**draft, "ClientId": "0", "submit": "draft"})
check(s == 200 and "Selecciona un cliente" in text(raw), "valida que haya cliente", f"HTTP {s}")
s, raw, _ = post("/Invoices/Create", "/Invoices/Create",
                 {**draft, "DueDate": (today - dt.timedelta(days=3)).isoformat(), "submit": "draft"})
check(s == 200 and "no puede ser anterior a la emisión" in text(raw), "valida vencimiento posterior a la emisión")
s, raw, _ = post("/Invoices/Create", "/Invoices/Create",
                 {**draft, "Lines[0].Description": "", "Lines[2].Description": "", "submit": "draft"})
check(s == 200 and "al menos un concepto" in text(raw), "exige al menos un concepto")
s, raw, _ = post("/Invoices/Create", "/Invoices/Create",
                 {**draft, "IssueDate": (today - dt.timedelta(days=400)).isoformat(), "submit": "issue"})
check(s == 200 and "última factura de la serie" in text(raw), "impide romper el orden de la serie")

s, _, h = post("/Invoices/Create", "/Invoices/Create", {**draft, "submit": "draft"})
inv = int(re.search(r"/Invoices/Details/(\d+)", location(h)).group(1)) if s == 302 else 0
check(s == 302 and inv > 0, "guardar borrador", f"HTTP {s}")
page = text(get(f"/Invoices/Details/{inv}")[1])
check("Borrador de factura" in page and "562,50 €" in page, "borrador con base 562,50 € (450 + 2,5 × 45)")
check("680,63 €" in page, "total con IVA 680,63 € (redondeo comercial)")

# Edición: sustituir conceptos (antes fallaba por conflicto de seguimiento en EF)
s, _, h = post(f"/Invoices/Edit/{inv}", f"/Invoices/Edit/{inv}", {
    **draft, "Id": str(inv), "TaxRate": "10", "submit": "draft",
    "Lines[0].Description": "Auditoría SEO técnica", "Lines[0].Quantity": "1", "Lines[0].UnitPrice": "500",
    "Lines[1].Description": "Informe de palabras clave", "Lines[1].Quantity": "1", "Lines[1].UnitPrice": "120",
    "Lines[2].Description": "", "Lines[2].Quantity": "1", "Lines[2].UnitPrice": "",
})
check(s == 302, "editar borrador cambiando conceptos", f"HTTP {s}")
page = text(get(f"/Invoices/Details/{inv}")[1])
check("620,00 €" in page and "682,00 €" in page and "Horas de soporte" not in page, "conceptos e IVA 10 % actualizados")

# Emisión: siguiente número de la serie
expected = f"FAC-{today.year}-{max(by_year.get(str(today.year), [0])) + 1:04d}"
s, _, _ = post(f"/Invoices/Details/{inv}", f"/Invoices/Issue/{inv}", {})
page = text(get(f"/Invoices/Details/{inv}")[1])
check(s == 302 and expected in page and "pill-pending" in page, f"emitir asigna {expected}", f"HTTP {s}")
s, _, _ = post(f"/Invoices/Details/{inv}", f"/Invoices/Delete/{inv}", {})
check(get(f"/Invoices/Details/{inv}")[0] == 200, "una factura emitida no se puede borrar")

# Cobro, deshacer y anulación
s, _, _ = post(f"/Invoices/Details/{inv}", f"/Invoices/RegisterPayment/{inv}",
               {"paidDate": (today + dt.timedelta(days=2)).isoformat(), "method": "Bizum"})
check("pill-paid" not in text(get(f"/Invoices/Details/{inv}")[1]), "rechaza fecha de cobro futura")
s, _, _ = post(f"/Invoices/Details/{inv}", f"/Invoices/RegisterPayment/{inv}", {"paidDate": iso, "method": "Bizum"})
page = text(get(f"/Invoices/Details/{inv}")[1])
check(s == 302 and "pill-paid" in page and "Bizum" in page, "registrar cobro")
check(get(f"/Invoices/Edit/{inv}")[0] == 302, "una factura cobrada no se edita")
post(f"/Invoices/Details/{inv}", f"/Invoices/UndoPayment/{inv}", {})
check("pill-pending" in text(get(f"/Invoices/Details/{inv}")[1]), "deshacer cobro")
post(f"/Invoices/Details/{inv}", f"/Invoices/Cancel/{inv}", {"reason": "Prueba de auditoría"})
page = text(get(f"/Invoices/Details/{inv}")[1])
check("pill-cancelled" in page and "Prueba de auditoría" in page, "anular con motivo")

# Emitir directamente y duplicar
s, _, h = post("/Invoices/Create", "/Invoices/Create", {**draft, "submit": "issue"})
inv2 = int(re.search(r"/Invoices/Details/(\d+)", location(h)).group(1)) if s == 302 else 0
nxt = f"FAC-{today.year}-{int(expected[-4:]) + 1:04d}"
check(inv2 and nxt in text(get(f"/Invoices/Details/{inv2}")[1]), f"emitir al crear asigna {nxt}")
s, raw, _ = get(f"/Invoices/Create?copyOf={inv2}")
check(s == 200 and "Copia de" in text(raw) and "Horas de soporte" in text(raw), "duplicar precarga los conceptos")

# Borrador eliminable
s, _, h = post("/Invoices/Create", "/Invoices/Create", {**draft, "submit": "draft"})
inv3 = int(re.search(r"/Invoices/Details/(\d+)", location(h)).group(1))
post(f"/Invoices/Details/{inv3}", f"/Invoices/Delete/{inv3}", {})
check(get(f"/Invoices/Details/{inv3}")[0] == 404, "eliminar borrador")

# Un cliente con facturas no se borra
post(f"/Clients/Details/{new_client}", f"/Clients/Delete/{new_client}", {})
check(get(f"/Clients/Details/{new_client}")[0] == 200, "no borra clientes con facturas")

# Formularios sin token
s, _, _ = request("POST", f"/Invoices/Issue/{inv2}", {})
check(s == 400, "rechaza POST sin token antiforgery", f"HTTP {s}")

print(f"\n{passed} comprobaciones correctas, {len(failures)} fallos")
summary = os.environ.get("GITHUB_STEP_SUMMARY")
if summary:
    with open(summary, "a", encoding="utf-8") as f:
        f.write(f"### Auditoría: {passed} OK, {len(failures)} fallos\n")
        for x in failures:
            f.write(f"- {x}\n")
for x in failures:
    print(f"::error::{x}")
sys.exit(1 if failures else 0)
