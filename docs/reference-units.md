# Selling unit reference data

For explicit production provisioning or SQL fallback, see
[database-provisioning.md](database-provisioning.md). The legacy local command below
retains its original guard. Both workflows use the same add-only unit definitions.

The Add Product query loads active `UnitsOfMeasure` rows from SQL Server on GET and after unsuccessful POSTs. The empty dropdown was caused by an empty table, not a binding failure.

Run the add-only local development setup command from the project directory:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile -- --seed-catalogue-units
```

This command is restricted to Windows-authenticated Emmanuel / IlliasuLocalMallDb in Development. It uses the application's DefaultConnection, a serializable transaction and a transaction-owned application lock. It never runs during ordinary startup or page loads. Repeat runs skip existing codes, including inactive or differently configured records; review those records explicitly rather than overwriting them.

| Name | Code / symbol | Existing kind | Decimal places |
|---|---|---|---|
| Piece | PCS | Individual | 0 |
| Pack | PACK | Pack | 0 |
| Carton | CTN | Carton | 0 |
| Tray | TRAY | Pack | 0 |
| Bag | BAG | Pack | 0 |
| Bottle | BTL | Individual | 0 |
| Kilogram | KG | Mass | 3 |
| Gram | G | Mass | 3 |
| Litre | L | Volume | 3 |
| Millilitre | ML | Volume | 3 |

Tray and Bag are packaged selling units; Bottle is one discrete item. No new enum values or tables are needed. Sealed bags remain whole packaged options; loose weighed goods use mass units. Measured units permit up to three decimals, but fractional sales still require the selling option's explicit permission and a compatible selling increment. Packaging units require whole quantities.

No product-specific conversion, stock mapping or stock balance is inferred or seeded. Staff explicitly define package contents and stock behaviour for each product. Existing catalogue and Identity records are preserved.

The form calls the field **Selling unit**, explains common choices and warns if no active units exist. Dynamic rows reuse the server-rendered active choices; adding a row does not reset existing selections. MVC retains selected values on validation errors.

Verification: `dotnet run --project tests/UnitReferenceSmoke`; the existing ProductCreateSmoke SQL/HTTP suite and its browser preview cover validation, packaging behaviour and selection retention.
