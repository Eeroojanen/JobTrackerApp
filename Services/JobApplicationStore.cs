using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JobTracker.Models;

namespace JobTracker.Services;

public class JobApplicationStore
{
    private readonly string _filePath;

    public JobApplicationStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobTracker"
        );

        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "applications.json");
    }

    public async Task<List<JobApplication>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new List<JobApplication>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<JobApplication>>(json) ?? new List<JobApplication>();
        }
        catch
        {
            return new List<JobApplication>();
        }
    }

    public async Task SaveAsync(IEnumerable<JobApplication> items)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_filePath, json);
    }
}