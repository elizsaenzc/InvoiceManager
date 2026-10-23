using InvoiceManager.Models;
using InvoiceManager.Services;

namespace InvoiceManager.Data;

/// <summary>
/// Datos de demostración de una agencia digital en A Coruña: clientes ficticios con CIF válido,
/// cuotas mensuales (mantenimiento, SEO, Ads, redes), proyectos con anticipo y entrega,
/// renovaciones anuales y un comportamiento de cobro distinto por cliente.
/// Las fechas son relativas a hoy, así que la demo siempre muestra los últimos 12 meses.
/// </summary>
public static class DbSeeder
{
    private record Service(string Description, decimal Price);

    private static readonly Dictionary<string, Service> S = new()
    {
        ["mant"]      = new("Mantenimiento web mensual · plan Profesional", 120m),
        ["hosting"]   = new("Hosting gestionado con certificado SSL (anual)", 189m),
        ["dominio"]   = new("Renovación de dominio .es (anual)", 12.90m),
        ["ads"]       = new("Gestión de campañas Google Ads (mensual)", 350m),
        ["seo"]       = new("Posicionamiento SEO local (mensual)", 290m),
        ["rrss"]      = new("Gestión de redes sociales · Instagram y Facebook (mensual)", 420m),
        ["web"]       = new("Desarrollo de web corporativa en WordPress (5 secciones)", 1850m),
        ["tienda"]    = new("Tienda online WooCommerce con pasarela Redsys", 3400m),
        ["branding"]  = new("Identidad corporativa y manual de marca", 950m),
        ["foto"]      = new("Sesión fotográfica de producto (media jornada)", 280m),
        ["soporte"]   = new("Soporte técnico fuera de plan (horas)", 45m),
        ["workspace"] = new("Licencias Google Workspace Business Starter (mensual)", 6.80m),
        ["reservas"]  = new("Módulo de reservas online con confirmación por email", 1250m),
        ["auditoria"] = new("Auditoría de accesibilidad web (WCAG 2.1 AA)", 690m),
        ["email"]     = new("Campaña de email marketing (diseño y envío)", 180m),
        ["video"]     = new("Vídeo promocional de 60 s con edición y subtítulos", 750m),
        ["carta"]     = new("Carta digital con código QR y actualización trimestral", 240m),
    };

    /// <param name="Delay">Días medios que tarda en pagar respecto al vencimiento (negativo = antes).</param>
    private record ClientPlan(Client Client, string Method, int Delay, int Spread,
                        (string Key, decimal Qty)[] Retainer, int RetainerFrom,
                        (string Key, int MonthsAgo)[] Projects, int WebAnniversary);

    public static void Seed(AppDbContext db)
    {
        if (db.Clients.Any()) return;

        var rnd = new Random(2026);
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

        Client C(string name, string contact, string email, string phone, string address, string city,
                 char letter, string digits, int terms, int monthsAgo) => new()
        {
            Name = name, ContactName = contact, Email = email, Phone = phone, Address = address, City = city,
            TaxId = Cif(letter, digits), PaymentTermsDays = terms, CreatedAt = start.AddMonths(-monthsAgo).ToUniversalTime()
        };

        var seeds = new List<ClientPlan>
        {
            new(C("Clínica Dental Riveiro S.L.P.", "Marta Riveiro", "administracion@clinicariveiro.es", "+34 982 241 563",
                  "Rúa da Raíña 12, bajo", "27001 Lugo", 'B', "2741563", 30, 14),
                "Domiciliación SEPA", -2, 2, new[] { ("mant", 1m), ("seo", 1m) }, 11,
                new[] { ("auditoria", 9), ("foto", 4) }, 3),
            new(C("Hotel Mirador de Sanxenxo S.L.", "Iago Fernández", "direccion@hotelmiradorsanxenxo.com", "+34 986 720 118",
                  "Avenida de Silgar 41", "36960 Sanxenxo", 'B', "3672011", 30, 20),
                "Transferencia", 6, 8, new[] { ("ads", 1m), ("rrss", 1m) }, 11,
                new[] { ("reservas", 10), ("video", 3) }, 7),
            new(C("Castro & Asociados Asesoría Fiscal S.L.", "Pilar Castro", "pcastro@castroasesores.es", "+34 982 252 940",
                  "Praza de Santo Domingo 8, 2.º", "27001 Lugo", 'B', "2725294", 15, 13),
                "Domiciliación SEPA", 0, 1, new[] { ("mant", 1m), ("workspace", 8m) }, 11,
                new[] { ("web", 12) }, 0),
            new(C("Taberna do Porto Hostalería S.L.", "Xurxo Lema", "ola@tabernadoporto.gal", "+34 981 905 274",
                  "Rúa da Galera 23", "15003 A Coruña", 'B', "1590527", 15, 11),
                "Bizum", 2, 4, new[] { ("rrss", 1m) }, 10,
                new[] { ("carta", 8), ("foto", 2) }, 0),
            new(C("Talleres Mecánicos Pazos e Fillos S.L.", "Manuel Pazos", "talleres@pazosefillos.es", "+34 988 247 336",
                  "Polígono de San Cibrao, rúa 6, nave 14", "32901 San Cibrao das Viñas", 'B', "3224733", 30, 8),
                "Transferencia", 28, 14, new[] { ("mant", 1m) }, 5,
                new[] { ("web", 7) }, 0),
            new(C("Conservas Rías Baixas S.A.", "Lucía Otero", "compras@conservasriasbaixas.com", "+34 986 511 803",
                  "Porto de Vilaxoán, nave 3", "36611 Vilagarcía de Arousa", 'A', "3651180", 60, 16),
                "Transferencia", 3, 5, new[] { ("ads", 1m), ("seo", 1m) }, 7,
                new[] { ("tienda", 10) }, 2),
            new(C("Academia de Idiomas Linguae S.L.", "Sara Méndez", "info@linguaesantiago.es", "+34 981 563 902",
                  "Rúa do Hórreo 64, 1.º", "15702 Santiago de Compostela", 'B', "1556390", 30, 12),
                "Tarjeta", 0, 3, new[] { ("ads", 1m) }, 11,
                new[] { ("email", 9), ("email", 5), ("email", 1) }, 0),
            new(C("Inmobiliaria Atlántica Coruña S.L.", "Roberto Varela", "rvarela@atlanticacoruna.es", "+34 981 128 455",
                  "Avenida de la Marina 9", "15001 A Coruña", 'B', "1512845", 30, 22),
                "Domiciliación SEPA", -1, 1, new[] { ("seo", 1m), ("mant", 1m), ("workspace", 5m) }, 11,
                Array.Empty<(string, int)>(), 5),
            new(C("Forza Lugo Fitness S.L.", "Antón Rodríguez", "hola@forzalugo.com", "+34 982 803 117",
                  "Rúa Montevideo 4", "27002 Lugo", 'B', "2780311", 15, 6),
                "Tarjeta", 4, 4, new[] { ("rrss", 1m) }, 4,
                new[] { ("branding", 6), ("web", 5) }, 0),
            new(C("Ferretería Industrial Miño S.A.", "Rosa Prado", "administracion@ferreteriamino.es", "+34 982 402 690",
                  "Rúa Cardeal 22", "27400 Monforte de Lemos", 'A', "2740269", 30, 4),
                "Transferencia", 1, 3, Array.Empty<(string, decimal)>(), 0,
                new[] { ("tienda", 3) }, 0),
            new(C("Varela Otero Abogados S.L.P.", "Beatriz Varela", "despacho@varelaotero.es", "+34 981 577 340",
                  "Rúa do Vilar 30, 1.º", "15705 Santiago de Compostela", 'B', "1557734", 30, 15),
                "Transferencia", -3, 2, new[] { ("mant", 1m), ("workspace", 4m) }, 9,
                new[] { ("web", 10) }, 0),
            new(C("Óptica Visión Compostela S.L.", "Diego Iglesias", "optica@visioncompostela.es", "+34 981 594 226",
                  "Rúa Nova 17", "15705 Santiago de Compostela", 'B', "1559422", 30, 18),
                "Domiciliación SEPA", 0, 1, new[] { ("seo", 1m) }, 11,
                new[] { ("foto", 5) }, 9),
            new(C("Construcciones Seoane S.L.", "José Seoane", "obras@construccionesseoane.es", "+34 986 780 552",
                  "Avenida de Buenos Aires 112", "36500 Lalín", 'B', "3678055", 30, 3),
                "Transferencia", 95, 5, Array.Empty<(string, decimal)>(), 0,
                new[] { ("web", 3) }, 0),
            new(C("Panadería O Forno da Mariña S.L.", "Carmen Ledo", "pedidos@fornodamarina.gal", "+34 982 560 384",
                  "Rúa Pastor Díaz 51", "27850 Viveiro", 'B', "2756038", 15, 1),
                "Bizum", 0, 2, Array.Empty<(string, decimal)>(), 0,
                new[] { ("branding", 1) }, 0),
        };

        db.Clients.AddRange(seeds.Select(s => s.Client));
        db.SaveChanges();

        var invoices = new List<Invoice>();

        Invoice NewInvoice(ClientPlan s, DateTime issue, params InvoiceLine[] lines) => new()
        {
            ClientId = s.Client.Id,
            IssueDate = issue,
            DueDate = issue.AddDays(s.Client.PaymentTermsDays),
            TaxRate = 21m,
            Lines = lines.ToList(),
            CreatedAt = issue.AddHours(9 + rnd.Next(0, 8)).ToUniversalTime(),
        };

        InvoiceLine L(string key, decimal qty = 1, string? prefix = null, decimal factor = 1) =>
            new() { Description = (prefix ?? "") + S[key].Description, Quantity = qty, UnitPrice = Math.Round(S[key].Price * factor, 2) };

        foreach (var s in seeds)
        {
            // Cuotas mensuales: primer día laborable de cada mes
            for (var m = s.RetainerFrom; m >= 0 && s.Retainer.Length > 0; m--)
            {
                var issue = FirstWorkingDay(new DateTime(today.Year, today.Month, 1).AddMonths(-m));
                if (issue > today) continue;
                var lines = s.Retainer.Select(r => L(r.Key, r.Qty)).ToList();
                if (rnd.NextDouble() < 0.18) lines.Add(L("soporte", rnd.Next(1, 5)));
                if (s.WebAnniversary > 0 && issue.Month == new DateTime(today.Year, today.Month, 1).AddMonths(-s.WebAnniversary).Month)
                {
                    lines.Add(L("hosting"));
                    lines.Add(L("dominio"));
                }
                invoices.Add(NewInvoice(s, issue, lines.ToArray()));
            }

            // Proyectos: 50 % de anticipo al empezar y 50 % a la entrega (un mes después)
            foreach (var (key, monthsAgo) in s.Projects)
            {
                var day = Math.Min(8 + rnd.Next(0, 14), 28);
                var startDate = Weekday(new DateTime(today.Year, today.Month, 1).AddMonths(-monthsAgo).AddDays(day - 1));
                if (startDate > today) startDate = Weekday(today.AddDays(-3));
                var big = S[key].Price >= 900;
                if (!big)
                {
                    invoices.Add(NewInvoice(s, startDate, L(key)));
                    continue;
                }
                invoices.Add(NewInvoice(s, startDate, L(key, 1, "Anticipo 50 % · ", 0.5m)));
                var delivery = Weekday(startDate.AddDays(30 + rnd.Next(0, 12)));
                if (delivery <= today)
                    invoices.Add(NewInvoice(s, delivery, L(key, 1, "Entrega final 50 % · ", 0.5m)));
            }
        }

        // Estado de cobro según el comportamiento de cada cliente
        var bySeed = seeds.ToDictionary(p => p.Client.Id);
        foreach (var inv in invoices)
        {
            var plan = bySeed[inv.ClientId];
            var paid = inv.DueDate.AddDays(plan.Delay + rnd.Next(-plan.Spread, plan.Spread + 1));
            if (paid < inv.IssueDate) paid = inv.IssueDate.AddDays(rnd.Next(1, 4));
            if (paid <= today)
            {
                inv.Status = InvoiceStatus.Paid;
                inv.PaidDate = paid;
                inv.PaymentMethod = plan.Method;
            }
            else
            {
                inv.Status = InvoiceStatusService.OpenStatusFor(inv.DueDate);
            }
        }

        // Una factura anulada por error en el importe (consume número, como exige la serie correlativa)
        var hotel = seeds[1].Client.Id;
        var wrong = invoices.Where(i => i.ClientId == hotel && i.Status == InvoiceStatus.Paid)
                            .OrderBy(i => i.IssueDate).Skip(4).First();
        invoices.Add(new Invoice
        {
            ClientId = hotel, IssueDate = wrong.IssueDate, DueDate = wrong.DueDate, TaxRate = 21m,
            Status = InvoiceStatus.Cancelled, CreatedAt = wrong.CreatedAt.AddMinutes(-40),
            Notes = "Anulada: importe de Google Ads duplicado. Sustituida por la factura siguiente.",
            Lines = { L("ads", 2), L("rrss") },
        });

        // Dos borradores en preparación
        var forno = seeds[13];
        invoices.Add(new Invoice
        {
            ClientId = forno.Client.Id, IssueDate = today, DueDate = today.AddDays(forno.Client.PaymentTermsDays),
            Status = InvoiceStatus.Draft, Notes = "Pendiente de confirmar el número de secciones con Carmen.",
            Lines = { L("web", 1, "Anticipo 50 % · ", 0.5m), L("foto") },
        });
        var atlantica = seeds[7];
        invoices.Add(new Invoice
        {
            ClientId = atlantica.Client.Id, IssueDate = today, DueDate = today.AddDays(atlantica.Client.PaymentTermsDays),
            Status = InvoiceStatus.Draft,
            Lines = { L("soporte", 6), new InvoiceLine { Description = "Migración de fichas de inmuebles al nuevo CRM", Quantity = 1, UnitPrice = 480m } },
        });

        // Numeración correlativa por año en orden de emisión
        var seq = new Dictionary<int, int>();
        foreach (var inv in invoices.Where(i => i.Status != InvoiceStatus.Draft).OrderBy(i => i.IssueDate).ThenBy(i => i.CreatedAt))
        {
            var y = inv.IssueDate.Year;
            seq[y] = seq.GetValueOrDefault(y) + 1;
            inv.Number = $"{InvoiceNumberService.Prefix(y)}{seq[y]:D4}";
            inv.UpdatedAt = (inv.PaidDate ?? inv.IssueDate).AddHours(10).ToUniversalTime();
        }

        db.Invoices.AddRange(invoices);
        db.SaveChanges();
    }

    private static DateTime FirstWorkingDay(DateTime monthStart) => Weekday(monthStart);

    private static DateTime Weekday(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(2),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d
    };

    /// <summary>CIF con dígito de control correcto (sociedades A y B).</summary>
    private static string Cif(char letter, string digits)
    {
        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var n = digits[i] - '0';
            if (i % 2 == 0) { n *= 2; sum += n / 10 + n % 10; }
            else sum += n;
        }
        return $"{letter}{digits}{(10 - sum % 10) % 10}";
    }
}
