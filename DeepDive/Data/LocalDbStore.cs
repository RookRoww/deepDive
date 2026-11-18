using System.Data;
using Microsoft.Data.SqlClient;

namespace DeepDive;

public sealed class LocalDbStore(DbConnector connector) : IStore
{
    public IReadOnlyList<Space> GetSpaces()
    {
        using var connection = connector.Create();
        connection.Open();
        using var command = new SqlCommand("""
            SELECT w.WorkspaceId, w.WorkspaceName, w.ColorHex, w.IconKey, w.DisplayOrder,
                   w.CreatedAt, w.UpdatedAt, a.ActionId, a.ActionTypeId, a.ActionName,
                   a.TargetPath, a.ActionOrder, a.IsEnabled, t.TypeCode, t.DisplayName, t.AllowedFormat
            FROM dbo.Workspaces AS w
            LEFT JOIN dbo.WorkspaceActions AS a ON a.WorkspaceId = w.WorkspaceId
            LEFT JOIN dbo.ActionTypes AS t ON t.ActionTypeId = a.ActionTypeId
            ORDER BY w.DisplayOrder, w.WorkspaceId, a.ActionOrder, a.ActionId;
            """, connection);
        using var reader = command.ExecuteReader();
        var spaces = new List<Space>();
        Space? space = null;
        while (reader.Read())
        {
            int spaceId = reader.GetInt32(0);
            if (space is null || space.Id != spaceId)
            {
                space = new Space
                {
                    Id = spaceId,
                    Name = reader.GetString(1),
                    Color = ReadText(reader, 2),
                    Icon = ReadText(reader, 3),
                    Order = reader.GetInt32(4),
                    Created = reader.GetDateTime(5),
                    Updated = reader.GetDateTime(6)
                };
                spaces.Add(space);
            }

            if (!reader.IsDBNull(7))
            {
                int typeId = reader.GetInt32(8);
                space.Items.Add(new Item
                {
                    Id = reader.GetInt32(7),
                    SpaceId = spaceId,
                    TypeId = typeId,
                    Name = reader.GetString(9),
                    Target = ReadText(reader, 10),
                    Order = reader.GetInt32(11),
                    Enabled = reader.GetBoolean(12),
                    Type = ReadType(reader, typeId, 13)
                });
            }
        }
        return spaces;
    }

    public IReadOnlyList<ItemType> GetTypes()
    {
        using var connection = connector.Create();
        connection.Open();
        using var command = new SqlCommand("""
            SELECT ActionTypeId, TypeCode, DisplayName, AllowedFormat
            FROM dbo.ActionTypes ORDER BY ActionTypeId;
            """, connection);
        using var reader = command.ExecuteReader();
        var types = new List<ItemType>();
        while (reader.Read())
            types.Add(ReadType(reader, reader.GetInt32(0), 1));
        return types;
    }

    public void CheckResultTable()
    {
        using var connection = connector.Create();
        connection.Open();
        using var command = new SqlCommand("""
            SELECT TOP (0) ResultId, WorkspaceId, ActionId, LaunchedAt,
                           StatusCode, StatusMessage, NeedsAttention
            FROM dbo.LaunchResults;
            """, connection);
        using var reader = command.ExecuteReader();
    }

    public void SaveSpace(Space space)
    {
        using var connection = connector.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.Now;
        int id;
        int order;
        DateTime created;
        DateTime updated;
        using (var command = new SqlCommand(space.Id == 0 ? InsertSpace : UpdateSpace, connection, transaction))
        {
            command.Parameters.Add("@id", SqlDbType.Int).Value = space.Id;
            command.Parameters.Add("@name", SqlDbType.VarChar, 100).Value = space.Name;
            command.Parameters.Add("@color", SqlDbType.VarChar, 7).Value = space.Color;
            command.Parameters.Add("@icon", SqlDbType.VarChar, 50).Value = space.Icon;
            command.Parameters.Add("@now", SqlDbType.DateTime).Value = now;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("That workspace no longer exists.");
            id = reader.GetInt32(0);
            order = reader.GetInt32(1);
            created = reader.GetDateTime(2);
            updated = reader.GetDateTime(3);
        }

        var removed = new HashSet<int>();
        using (var command = new SqlCommand("SELECT ActionId FROM dbo.WorkspaceActions WHERE WorkspaceId = @space;", connection, transaction))
        {
            command.Parameters.Add("@space", SqlDbType.Int).Value = id;
            using var reader = command.ExecuteReader();
            while (reader.Read())
                removed.Add(reader.GetInt32(0));
        }

        var itemIds = new List<int>(space.Items.Count);
        foreach (var item in space.Items)
        {
            if (item.Id != 0 && !removed.Remove(item.Id))
                throw new InvalidOperationException("An item no longer belongs to this workspace.");
            itemIds.Add(SaveItem(connection, transaction, id, item, itemIds.Count + 1));
        }

        if (removed.Count > 0)
        {
            using var command = new SqlCommand("""
                DELETE FROM dbo.LaunchResults WHERE WorkspaceId = @space AND ActionId = @item;
                DELETE FROM dbo.WorkspaceActions WHERE WorkspaceId = @space AND ActionId = @item;
                """, connection, transaction);
            command.Parameters.Add("@space", SqlDbType.Int).Value = id;
            var itemParameter = command.Parameters.Add("@item", SqlDbType.Int);
            foreach (int itemId in removed)
            {
                itemParameter.Value = itemId;
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
        space.Id = id;
        space.Order = order;
        space.Created = created;
        space.Updated = updated;
        for (int i = 0; i < space.Items.Count; i++)
        {
            space.Items[i].Id = itemIds[i];
            space.Items[i].SpaceId = id;
            space.Items[i].Order = i + 1;
        }
    }

    public void DeleteSpace(int id)
    {
        using var connection = connector.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("""
            SELECT WorkspaceId FROM dbo.Workspaces WITH (UPDLOCK, HOLDLOCK) WHERE WorkspaceId = @id;
            DELETE FROM dbo.LaunchResults WHERE WorkspaceId = @id;
            DELETE FROM dbo.WorkspaceActions WHERE WorkspaceId = @id;
            DELETE FROM dbo.Workspaces WHERE WorkspaceId = @id;
            """, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SaveOrder(IReadOnlyList<int> ids)
    {
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("A workspace can appear only once in the order.", nameof(ids));
        using var connection = connector.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("""
            UPDATE dbo.Workspaces SET DisplayOrder = @order WHERE WorkspaceId = @id;
            IF @@ROWCOUNT = 0 THROW 50000, 'That workspace no longer exists.', 1;
            """, connection, transaction);
        var idParameter = command.Parameters.Add("@id", SqlDbType.Int);
        var orderParameter = command.Parameters.Add("@order", SqlDbType.Int);
        for (int i = 0; i < ids.Count; i++)
        {
            idParameter.Value = ids[i];
            orderParameter.Value = i + 1;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void SaveResults(IReadOnlyList<Result> results)
    {
        if (results.Count == 0)
            return;
        using var connection = connector.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = new SqlCommand("""
            INSERT INTO dbo.LaunchResults (WorkspaceId, ActionId, LaunchedAt, StatusCode, StatusMessage, NeedsAttention)
            SELECT @space, @item, @time, @code, @message, @needsFix
            FROM dbo.WorkspaceActions
            WHERE WorkspaceId = @space AND ActionId = @item;
            IF @@ROWCOUNT = 0 THROW 50001, 'The item no longer belongs to the workspace.', 1;
            """, connection, transaction);
        var space = command.Parameters.Add("@space", SqlDbType.Int);
        var item = command.Parameters.Add("@item", SqlDbType.Int);
        var time = command.Parameters.Add("@time", SqlDbType.DateTime);
        var code = command.Parameters.Add("@code", SqlDbType.VarChar, 30);
        var message = command.Parameters.Add("@message", SqlDbType.VarChar, 255);
        var needsFix = command.Parameters.Add("@needsFix", SqlDbType.Bit);
        foreach (var result in results)
        {
            space.Value = result.SpaceId;
            item.Value = result.ItemId;
            time.Value = result.Time;
            code.Value = result.Code;
            message.Value = result.Message;
            needsFix.Value = result.NeedsFix;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static int SaveItem(SqlConnection connection, SqlTransaction transaction, int spaceId, Item item, int order)
    {
        using var command = new SqlCommand(item.Id == 0 ? InsertItem : UpdateItem, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = item.Id;
        command.Parameters.Add("@space", SqlDbType.Int).Value = spaceId;
        command.Parameters.Add("@type", SqlDbType.Int).Value = item.TypeId;
        command.Parameters.Add("@name", SqlDbType.VarChar, 100).Value = item.Name;
        command.Parameters.Add("@target", SqlDbType.VarChar, 1000).Value = item.Target;
        command.Parameters.Add("@order", SqlDbType.Int).Value = order;
        command.Parameters.Add("@enabled", SqlDbType.Bit).Value = item.Enabled;
        if (command.ExecuteScalar() is not int id)
            throw new InvalidOperationException("An item no longer belongs to this workspace.");
        return id;
    }

    private static string ReadText(SqlDataReader reader, int index) => reader.IsDBNull(index) ? "" : reader.GetString(index);

    private static ItemType ReadType(SqlDataReader reader, int id, int index) =>
        new(id, reader.GetString(index), reader.GetString(index + 1), ReadText(reader, index + 2));

    private const string InsertSpace = """
        INSERT INTO dbo.Workspaces (WorkspaceName, ColorHex, IconKey, DisplayOrder, CreatedAt, UpdatedAt)
        OUTPUT INSERTED.WorkspaceId, INSERTED.DisplayOrder, INSERTED.CreatedAt, INSERTED.UpdatedAt
        SELECT @name, @color, @icon, COALESCE(MAX(DisplayOrder), 0) + 1, @now, @now
        FROM dbo.Workspaces WITH (UPDLOCK, HOLDLOCK);
        """;

    private const string UpdateSpace = """
        UPDATE dbo.Workspaces SET WorkspaceName = @name, ColorHex = @color, IconKey = @icon, UpdatedAt = @now
        OUTPUT INSERTED.WorkspaceId, INSERTED.DisplayOrder, INSERTED.CreatedAt, INSERTED.UpdatedAt
        WHERE WorkspaceId = @id;
        """;

    private const string InsertItem = """
        INSERT INTO dbo.WorkspaceActions (WorkspaceId, ActionTypeId, ActionName, TargetPath, ActionOrder, IsEnabled)
        OUTPUT INSERTED.ActionId
        VALUES (@space, @type, @name, @target, @order, @enabled);
        """;

    private const string UpdateItem = """
        UPDATE dbo.WorkspaceActions
        SET ActionTypeId = @type, ActionName = @name, TargetPath = @target, ActionOrder = @order, IsEnabled = @enabled
        OUTPUT INSERTED.ActionId
        WHERE WorkspaceId = @space AND ActionId = @id;
        """;
}
