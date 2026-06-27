using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HtmlAgilityPack;
using PdfDownloader.Models;

namespace PdfDownloader.Views
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new();
        private readonly ObservableCollection<PdfItem> _pdfItems = new();

        public MainWindow()
        {
            InitializeComponent();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            PdfListBox.ItemsSource = _pdfItems;
            
            // Wire events programmatically to avoid compile-time binding generator errors
            ScrapeButton.Click += OnScrapeClick;
            DownloadButton.Click += OnDownloadClick;
            
            // Custom Selection change handler to enable/disable the download button
            PdfListBox.SelectionChanged += (s, e) =>
            {
                DownloadButton.IsEnabled = PdfListBox.SelectedItems?.Count > 0;
            };
        }

        private async void OnScrapeClick(object? sender, RoutedEventArgs e)
        {
            var url = UrlInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                UpdateStatus("Error: Please enter a valid URL.", true);
                return;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
                UrlInput.Text = url;
            }

            try
            {
                UpdateStatus("Fetching webpage...", false);
                _pdfItems.Clear();

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                var htmlContent = await response.Content.ReadAsStringAsync();
                
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var baseUri = new Uri(finalUrl);
                
                // Handle <base href="..."> if present
                var baseNode = doc.DocumentNode.SelectSingleNode("//base[@href]");
                if (baseNode != null)
                {
                    var baseHref = baseNode.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrWhiteSpace(baseHref))
                    {
                        try
                        {
                            baseUri = new Uri(baseUri, baseHref);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to parse base href '{baseHref}': {ex.Message}");
                        }
                    }
                }

                var links = doc.DocumentNode.SelectNodes("//a[@href]");
                
                if (links == null)
                {
                    UpdateStatus("No PDF files found on this page.", false);
                    return;
                }

                int count = 0;
                foreach (var linkNode in links)
                {
                    var href = linkNode.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        // Clean query parameters and fragments to check for .pdf extension
                        var cleanHref = href.Split('?')[0].Split('#')[0].Trim();
                        if (cleanHref.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            // Convert relative links to absolute URLs
                            var absoluteUrl = new Uri(baseUri, href).AbsoluteUri;
                            
                            // Extract human-readable text or fallback to filename
                            var text = linkNode.InnerText?.Trim();
                            if (string.IsNullOrEmpty(text))
                            {
                                text = Path.GetFileName(cleanHref);
                            }

                            // Make sure file extension is present in title
                            if (!text.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                text += ".pdf";
                            }

                            // Prevent duplicates
                            if (!_pdfItems.Any(item => item.Url == absoluteUrl))
                            {
                                _pdfItems.Add(new PdfItem { Title = text, Url = absoluteUrl });
                                count++;
                            }
                        }
                    }
                }

                if (count > 0)
                {
                    UpdateStatus($"Successfully scraped {count} PDF(s). Select files to download.", false);
                }
                else
                {
                    UpdateStatus("No PDF files found on this page.", false);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Scraping failed: {ex.Message}", true);
            }
        }

        private async void OnDownloadClick(object? sender, RoutedEventArgs e)
        {
            var selectedItems = PdfListBox.SelectedItems?.Cast<PdfItem>().ToList();
            if (selectedItems == null || !selectedItems.Any())
            {
                UpdateStatus("Error: Please select at least one PDF.", true);
                return;
            }

            // Open Native Folder Picker
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var folderResults = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Destination Folder",
                AllowMultiple = false
            });

            if (folderResults == null || !folderResults.Any())
            {
                return; // User cancelled the folder selection
            }

            var destFolderUri = folderResults[0].Path;
            var destFolderPath = destFolderUri.LocalPath;

            // Start Batch Downloads
            SetUiState(isDownloading: true);
            int total = selectedItems.Count;
            int successCount = 0;
            var failedItems = new List<(string Title, string Error)>();

            for (int i = 0; i < total; i++)
            {
                var item = selectedItems[i];
                UpdateStatus($"Downloading [{i + 1}/{total}]: {item.Title}", false);
                
                try
                {
                    // Securely sanitize download path
                    var cleanFileName = GetSafeFilename(item.Title);
                    var fullPath = Path.Combine(destFolderPath, cleanFileName);

                    // Stream file to avoid heavy memory allocation
                    using var responseStream = await _httpClient.GetStreamAsync(item.Url);
                    using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                    
                    await responseStream.CopyToAsync(fileStream);
                    successCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to download {item.Title}: {ex.Message}");
                    failedItems.Add((item.Title, ex.Message));
                }

                // Update Progress bar percentage
                DownloadProgressBar.Value = (double)(i + 1) / total * 100;
            }

            SetUiState(isDownloading: false);
            if (failedItems.Count > 0)
            {
                var sampleError = failedItems[0].Error;
                UpdateStatus($"Downloaded {successCount} of {total} PDFs. First error: {sampleError}", true);
            }
            else
            {
                UpdateStatus($"Done! Downloaded {successCount} of {total} PDFs successfully.", false);
            }
        }

        private void UpdateStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError ? 
                Avalonia.Media.Brushes.Tomato : 
                Avalonia.Media.Brushes.LightGray;
        }

        private void SetUiState(bool isDownloading)
        {
            ScrapeButton.IsEnabled = !isDownloading;
            DownloadButton.IsEnabled = !isDownloading;
            UrlInput.IsEnabled = !isDownloading;
            PdfListBox.IsEnabled = !isDownloading;
            DownloadProgressBar.IsVisible = isDownloading;
            
            if (isDownloading)
            {
                DownloadProgressBar.Value = 0;
            }
        }

        private string GetSafeFilename(string filename)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c, '_');
            }
            return filename;
        }
    }
}
