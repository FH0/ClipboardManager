using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Dapper;

namespace ClipboardManager.Services
{
    public class ClipboardItem
    {
        public long Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString()
        {
            return Content ?? string.Empty;
        }
    }

    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private const int MaxItems = 50000;

        public DatabaseService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            _dbPath = Path.Combine(appDataPath, "clipboard.db");
            _connectionString = $"Data Source={_dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Items (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Content TEXT UNIQUE NOT NULL,
                        CreatedAt DATETIME NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS IDX_Items_CreatedAt ON Items(CreatedAt DESC);
                    CREATE INDEX IF NOT EXISTS IDX_Items_Content ON Items(Content);
                ";
                connection.Execute(createTableQuery);
            }
        }

        public void AddOrUpdateItem(string content)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var sql = @"
                        INSERT INTO Items (Content, CreatedAt) 
                        VALUES (@Content, @CreatedAt)
                        ON CONFLICT(Content) DO UPDATE SET CreatedAt = @CreatedAt;
                    ";
                    connection.Execute(sql, new { Content = content, CreatedAt = DateTime.UtcNow }, transaction);

                    // Enforce MaxItems limit
                    var deleteSql = @"
                        DELETE FROM Items 
                        WHERE Id IN (
                            SELECT Id FROM Items 
                            ORDER BY CreatedAt DESC 
                            LIMIT -1 OFFSET @MaxLimit
                        );
                    ";
                    connection.Execute(deleteSql, new { MaxLimit = MaxItems }, transaction);
                    
                    transaction.Commit();
                }
            }
        }

        public List<ClipboardItem> GetItems(string searchQuery = "", int limit = 100)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    return connection.Query<ClipboardItem>(
                        "SELECT * FROM Items ORDER BY CreatedAt DESC LIMIT @Limit", 
                        new { Limit = limit }).ToList();
                }
                else
                {
                    // Full text search could be optimized, but LIKE is acceptable for simple queries
                    var query = "%" + searchQuery + "%";
                    return connection.Query<ClipboardItem>(
                        "SELECT * FROM Items WHERE Content LIKE @SearchQuery ORDER BY CreatedAt DESC LIMIT @Limit", 
                        new { SearchQuery = query, Limit = limit }).ToList();
                }
            }
        }
    }
}
