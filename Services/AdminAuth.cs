using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ProfixmedWeb.Services;

/// <summary>"Admin" section of appsettings. The panel is disabled until PasswordHash is set.</summary>
public sealed class AdminOptions
{
    /// <summary>URL segment of the hidden panel: https://site/{Path}.</summary>
    public string Path { get; set; } = "manage";
    public string Username { get; set; } = "admin";

    /// <summary>Output of `dotnet ProfixmedWeb.dll hash-password`.</summary>
    public string? PasswordHash { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(PasswordHash);

    public string NormalizedPath
    {
        get
        {
            var path = Path.Trim('/');
            if (!Regex.IsMatch(path, "^[A-Za-z0-9_-]+$"))
                throw new InvalidOperationException("Admin:Path may contain only latin letters, digits, '-' and '_'.");
            return path;
        }
    }
}

/// <summary>PBKDF2 password hashes in the form "pbkdf2-sha256$iterations$salt$hash".</summary>
public static class AdminPassword
{
    private const string Scheme = "pbkdf2-sha256";
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        var parts = stored?.Split('$');
        if (parts is not [Scheme, var iterationsText, var saltText, var hashText] || !int.TryParse(iterationsText, out var iterations))
            return false;

        try
        {
            var expected = Convert.FromBase64String(hashText);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(saltText), iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool UsernameMatches(string? entered, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(entered ?? "")),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    /// <summary>Console mode: `dotnet ProfixmedWeb.dll hash-password` asks for a password and prints its hash.</summary>
    public static void RunHashCommand()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var password = ReadHidden("Пароль: ");
        if (password.Length < 10)
        {
            Console.Error.WriteLine("Пароль должен быть не короче 10 символов.");
            Environment.ExitCode = 1;
            return;
        }
        if (ReadHidden("Ещё раз: ") != password)
        {
            Console.Error.WriteLine("Пароли не совпадают.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Вставьте в appsettings.Production.json → \"Admin\": { \"PasswordHash\": ... }");
        Console.WriteLine(Hash(password));
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        // PowerShell may prefix piped input with a BOM.
        if (Console.IsInputRedirected)
            return (Console.ReadLine() ?? "").TrimStart('﻿');

        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (text.Length > 0) text.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                text.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return text.ToString();
    }
}
