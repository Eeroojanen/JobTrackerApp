using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Controls;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExcelDataReader;
using JobTracker.Models;
using JobTracker.Services;

namespace JobTracker.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly JobApplicationStore _store = new();

    public ObservableCollection<JobApplication> Applications { get; } = new();

    public AvaloniaList<JobApplication> FilteredApplications { get; } = new();

    [ObservableProperty]
    private string companyName = "";

    [ObservableProperty]
    private ApplicationStatus selectedStatus = ApplicationStatus.Pending;

    [ObservableProperty]
    private JobApplication? selectedApplication;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private string importCompanyHeader = "CompanyName";

    [ObservableProperty]
    private string importStatusHeader = "Status";

    [ObservableProperty]
    private string importProceedLabel = "Proceed";

    [ObservableProperty]
    private string importPendingLabel = "Pending";

    [ObservableProperty]
    private string importRejectedLabel = "Rejected";

    public ApplicationStatus[] Statuses { get; } =
        Enum.GetValues(typeof(ApplicationStatus)).Cast<ApplicationStatus>().ToArray();

    public MainWindowViewModel()
    {
        _ = LoadAsync();
        Applications.CollectionChanged += async (_, __) => await SaveAsync();
        Applications.CollectionChanged += (_, __) => RefreshFilter();
    }

    private async Task LoadAsync()
    {
        var items = await _store.LoadAsync();
        Applications.Clear();
        foreach (var item in items.OrderByDescending(x => x.CreatedAt))
            Applications.Add(item);

        RefreshFilter();
    }

    private async Task SaveAsync()
    {
        await _store.SaveAsync(Applications);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var name = (CompanyName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        Applications.Insert(0, new JobApplication
        {
            CompanyName = name,
            Status = SelectedStatus,
            CreatedAt = DateTime.Now
        });

        RefreshFilter();

        CompanyName = "";
        SelectedStatus = ApplicationStatus.Pending;

        await SaveAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedApplication is null)
            return;

        Applications.Remove(SelectedApplication);
        SelectedApplication = null;

        RefreshFilter();

        await SaveAsync();
    }

    [RelayCommand]
    public async Task ClearAllAsync()
    {
        Applications.Clear();
        SelectedApplication = null;
        RefreshFilter();
        await SaveAsync();
    }

    public async Task StatusChangedAsync()
    {
        await SaveAsync();
    }

    public async Task ImportFromExcelAsync(
        string filePath,
        string companyHeader,
        string statusHeader,
        string proceedLabel,
        string pendingLabel,
        string rejectedLabel)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        var table = dataSet.Tables.Cast<DataTable>().FirstOrDefault();
        if (table is null)
            return;

        var companyColumn = table.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => string.Equals(c.ColumnName, companyHeader, StringComparison.OrdinalIgnoreCase));
        var statusColumn = table.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => string.Equals(c.ColumnName, statusHeader, StringComparison.OrdinalIgnoreCase));

        if (companyColumn is null || statusColumn is null)
            return;

        foreach (DataRow row in table.Rows)
        {
            var companyNameValue = row[companyColumn]?.ToString() ?? "";
            var statusValue = row[statusColumn]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(companyNameValue))
                continue;

            var normalizedStatus = statusValue.Trim();
            var status = ResolveStatus(normalizedStatus, proceedLabel, pendingLabel, rejectedLabel);

            Applications.Insert(0, new JobApplication
            {
                CompanyName = companyNameValue.Trim(),
                Status = status,
                CreatedAt = DateTime.Now
            });
        }

        RefreshFilter();

        await SaveAsync();
    }

    private static ApplicationStatus ResolveStatus(
        string statusValue,
        string proceedLabel,
        string pendingLabel,
        string rejectedLabel)
    {
        if (string.Equals(statusValue, proceedLabel, StringComparison.OrdinalIgnoreCase))
            return ApplicationStatus.Proceed;

        if (string.Equals(statusValue, pendingLabel, StringComparison.OrdinalIgnoreCase))
            return ApplicationStatus.Pending;

        if (string.Equals(statusValue, rejectedLabel, StringComparison.OrdinalIgnoreCase))
            return ApplicationStatus.Rejected;

        if (Enum.TryParse<ApplicationStatus>(statusValue, true, out var status))
            return status;

        return ApplicationStatus.Pending;
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var query = (SearchText ?? string.Empty).Trim();
        FilteredApplications.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var item in Applications)
                FilteredApplications.Add(item);
            return;
        }

        var normalizedQuery = query.ToLowerInvariant();
        foreach (var item in Applications)
        {
            if (item.CompanyName.ToLowerInvariant().Contains(normalizedQuery))
                FilteredApplications.Add(item);
        }
    }
}