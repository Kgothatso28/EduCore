using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.Json;

Console.WriteLine("EduCore DB Inspector");

var connArg = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ConnectionStrings__EduCoreDatabase") ?? Environment.GetEnvironmentVariable("EDUCORE_CONNECTION");
if (string.IsNullOrWhiteSpace(connArg))
{
    Console.WriteLine("Connection string not provided. Pass as first argument or set ConnectionStrings__EduCoreDatabase or EDUCORE_CONNECTION environment variable.");
    return 1;
}

using var conn = new SqlConnection(connArg);
await conn.OpenAsync();

var inventory = new DatabaseInventory { Server = conn.DataSource, Database = conn.Database, RetrievedAt = DateTime.UtcNow };

// Tables and columns
var tablesCmd = new SqlCommand(@"
SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE IN ('BASE TABLE','VIEW')
ORDER BY TABLE_SCHEMA, TABLE_NAME", conn);

using (var rdr = await tablesCmd.ExecuteReaderAsync())
{
    while (await rdr.ReadAsync())
    {
        var schema = rdr.GetString(0);
        var name = rdr.GetString(1);
        var type = rdr.GetString(2);
        inventory.Tables.Add(new TableInfo { Schema = schema, Name = name, Type = type });
    }
}

foreach (var table in inventory.Tables)
{
    var colCmd = new SqlCommand(@"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
ORDER BY ORDINAL_POSITION", conn);
    colCmd.Parameters.AddWithValue("@schema", table.Schema);
    colCmd.Parameters.AddWithValue("@table", table.Name);
    using var cr = await colCmd.ExecuteReaderAsync();
    while (await cr.ReadAsync())
    {
        var col = new ColumnInfo
        {
            Name = cr.GetString(0),
            DataType = cr.GetString(1),
            IsNullable = string.Equals(cr.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
            MaxLength = cr.IsDBNull(3) ? null : cr.GetInt32(3) as int?
        };
        table.Columns.Add(col);
    }
}

// Primary keys
var pkCmd = new SqlCommand(@"
SELECT tc.TABLE_SCHEMA, tc.TABLE_NAME, kcu.COLUMN_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, kcu.ORDINAL_POSITION", conn);
using (var pr = await pkCmd.ExecuteReaderAsync())
{
    while (await pr.ReadAsync())
    {
        var schema = pr.GetString(0);
        var table = pr.GetString(1);
        var col = pr.GetString(2);
        inventory.PrimaryKeys.Add(new KeyInfo { Schema = schema, Table = table, Column = col });
    }
}

// Foreign keys
var fkCmd = new SqlCommand(@"
SELECT fk.name AS FK_NAME, sch1.name AS FK_SCHEMA, tab1.name AS FK_TABLE, col1.name AS FK_COLUMN,
       sch2.name AS PK_SCHEMA, tab2.name AS PK_TABLE, col2.name AS PK_COLUMN
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables tab1 ON fkc.parent_object_id = tab1.object_id
INNER JOIN sys.columns col1 ON fkc.parent_object_id = col1.object_id AND fkc.parent_column_id = col1.column_id
INNER JOIN sys.tables tab2 ON fkc.referenced_object_id = tab2.object_id
INNER JOIN sys.columns col2 ON fkc.referenced_object_id = col2.object_id AND fkc.referenced_column_id = col2.column_id
INNER JOIN sys.schemas sch1 ON tab1.schema_id = sch1.schema_id
INNER JOIN sys.schemas sch2 ON tab2.schema_id = sch2.schema_id
ORDER BY fk.name", conn);
using (var fr = await fkCmd.ExecuteReaderAsync())
{
    while (await fr.ReadAsync())
    {
        inventory.ForeignKeys.Add(new ForeignKeyInfo
        {
            Name = fr.GetString(0),
            FromSchema = fr.GetString(1),
            FromTable = fr.GetString(2),
            FromColumn = fr.GetString(3),
            ToSchema = fr.GetString(4),
            ToTable = fr.GetString(5),
            ToColumn = fr.GetString(6)
        });
    }
}

// Indexes
var idxCmd = new SqlCommand(@"
SELECT sch.name AS schema_name, t.name AS table_name, ind.name AS index_name, ind.is_unique, col.name AS column_name
FROM sys.indexes ind
INNER JOIN sys.index_columns ic ON ind.object_id = ic.object_id AND ind.index_id = ic.index_id
INNER JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
INNER JOIN sys.tables t ON ind.object_id = t.object_id
INNER JOIN sys.schemas sch ON t.schema_id = sch.schema_id
WHERE ind.is_primary_key = 0
ORDER BY sch.name, t.name, ind.name, ic.key_ordinal", conn);
using (var ir = await idxCmd.ExecuteReaderAsync())
{
    while (await ir.ReadAsync())
    {
        inventory.Indexes.Add(new IndexInfo
        {
            Schema = ir.GetString(0),
            Table = ir.GetString(1),
            IndexName = ir.GetString(2),
            IsUnique = ir.GetBoolean(3),
            Column = ir.IsDBNull(4) ? null : ir.GetString(4)
        });
    }
}

// Views
var viewCmd = new SqlCommand(@"SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS ORDER BY TABLE_SCHEMA, TABLE_NAME", conn);
using (var vr = await viewCmd.ExecuteReaderAsync())
{
    while (await vr.ReadAsync())
    {
        inventory.Views.Add(new ViewInfo { Schema = vr.GetString(0), Name = vr.GetString(1) });
    }
}

// Stored procedures
var procCmd = new SqlCommand(@"SELECT SPECIFIC_SCHEMA, ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE' ORDER BY SPECIFIC_SCHEMA, ROUTINE_NAME", conn);
using (var prr = await procCmd.ExecuteReaderAsync())
{
    while (await prr.ReadAsync())
    {
        inventory.Procedures.Add(new ProcInfo { Schema = prr.GetString(0), Name = prr.GetString(1) });
    }
}

var outFile = Path.Combine(Directory.GetCurrentDirectory(), "database_inventory.json");
var json = JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(outFile, json);

Console.WriteLine($"Database inventory written to: {outFile}");
return 0;

// Models
public class DatabaseInventory
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public DateTime RetrievedAt { get; set; }
    public List<TableInfo> Tables { get; } = new();
    public List<KeyInfo> PrimaryKeys { get; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; } = new();
    public List<IndexInfo> Indexes { get; } = new();
    public List<ViewInfo> Views { get; } = new();
    public List<ProcInfo> Procedures { get; } = new();
}

public class TableInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}

public class KeyInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
}

public class ForeignKeyInfo
{
    public string Name { get; set; } = string.Empty;
    public string FromSchema { get; set; } = string.Empty;
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
}

public class IndexInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public string? Column { get; set; }
}

public class ViewInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ProcInfo
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
