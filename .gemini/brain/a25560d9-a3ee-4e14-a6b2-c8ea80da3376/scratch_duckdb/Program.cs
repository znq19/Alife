using System;
using DuckDB.NET.Data;

class Program
{
    static void Main()
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT array_cosine_similarity([1.0, 2.0]::FLOAT[2], [2.0, 3.0]::FLOAT[2]);";
        var result = command.ExecuteScalar();
        Console.WriteLine($"Cosine Similarity: {result}");
    }
}
