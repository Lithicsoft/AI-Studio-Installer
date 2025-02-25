using System.IO;
using System.Net;
using System.Windows;
using IWshRuntimeLibrary;
using File = System.IO.File;
using Path = System.IO.Path;
using System.IO.Compression;
using System.ComponentModel;
using System.Net.Http;
using MessageBox = ModernWpf.MessageBox;

namespace Lithicsoft_AI_Studio_Installer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool isWorking = false;
    private bool isInstalled = false;
    private string localFilePath = ".build";
    private string url = "https://raw.githubusercontent.com/Lithicsoft/Lithicsoft-Trainer-Studio/refs/heads/main/update.datas";

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadHtml();

            if (File.Exists(".build") && Directory.Exists("Lithicsoft AI Studio"))
            {
                UpdateBuildTitle();
                if (CheckForUpdates())
                {
                    ControlButton.Content = "Repair";
                    isInstalled = true;
                }
                else
                {
                    ControlButton.Content = "Update";
                    isInstalled = true;
                }
            }
            else
            {
                ControlButton.Content = "Install";
                isInstalled = false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during initialization: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Clipboard.SetText(ex.ToString());
        }
    }

    private async void LoadHtml()
    {
        string url = "https://raw.githubusercontent.com/Lithicsoft/Lithicsoft-Trainer-Studio/refs/heads/main/changelog.html";
        using (HttpClient client = new HttpClient())
        {
            try
            {
                string htmlContent = await client.GetStringAsync(url);
                webBrowser.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading page: " + ex.Message);
            }
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        ControlButton.IsEnabled = false;

        try
        {
            isWorking = true;
            await Task.Run(() => Dispatcher.Invoke(() => AIStudio()));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Clipboard.SetText(ex.ToString());
        }
    }

    private async Task AIStudio()
    {
        var progress = new Progress<(int percent, string message)>(report =>
        {
            Dispatcher.Invoke(() =>
            {
                ProcessPercent.Content = $"{report.percent}%";
                ProcessBar.Value = report.percent;
                Information.Content = report.message;
            });
        });

        await Task.Run(() => DownloadAndExtractFiles(progress));
    }

    private void DownloadAndExtractFiles(IProgress<(int percent, string message)> progress)
    {
        string destinationFolder = "Lithicsoft AI Studio";
        string zipFilePath = "downloaded.zip";

        try
        {
            using var webClient = new WebClient();
            string fileContents = webClient.DownloadString(url);
            string[] lines = fileContents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length > 0)
            {
                string latestBuild = lines[0];
                string downloadUrl = lines.Length > 1 ? lines[1] : string.Empty;

                File.WriteAllText(localFilePath, latestBuild);

                webClient.DownloadProgressChanged += (s, e) =>
                {
                    int percentComplete = (int)(e.ProgressPercentage * 0.5);
                    progress.Report((percentComplete, $"Downloading... {e.ProgressPercentage}%"));
                };

                webClient.DownloadFileCompleted += (s, e) =>
                {
                    progress.Report((50, "Download complete. Extracting..."));
                    ExtractFiles(zipFilePath, destinationFolder, progress);
                };

                webClient.DownloadFileAsync(new Uri(downloadUrl), zipFilePath);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error during download: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Clipboard.SetText(ex.ToString());
            });
        }
    }

    private void ExtractFiles(string zipFilePath, string destinationFolder, IProgress<(int percent, string message)> progress)
    {
        try
        {
            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                int totalFiles = archive.Entries.Count;
                int extractedFiles = 0;

                foreach (var entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(destinationFolder, entry.FullName);

                    string directoryPath = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    if (entry.Name == "")
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        entry.ExtractToFile(destinationPath, overwrite: true);
                        Thread.Sleep(50);
                    }

                    extractedFiles++;
                    int percentComplete = 50 + (int)((double)extractedFiles / totalFiles * 50);
                    progress.Report((percentComplete, $"Extracting {entry.FullName}..."));
                }
            }

            File.Delete(zipFilePath);

            if (!isInstalled)
            {
                DirectoryPermissionHelper.SetFullControlPermissions(destinationFolder);
                CreateShortcut("Lithicsoft AI Studio", Path.GetFullPath(Path.Combine(destinationFolder, "Lithicsoft AI Studio.exe")));

                Dispatcher.Invoke(() =>
                {
                    ShowNotification("Installation Complete", "Lithicsoft AI Studio has been installed!");
                    Information.Content = "Waiting ...";
                    ControlButton.Content = "Repair";
                    ControlButton.IsEnabled = true;
                    isWorking = false;
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    ShowNotification("Update Complete", "Lithicsoft AI Studio has been updated!");
                    Information.Content = "Waiting ...";
                    ControlButton.Content = "Repair";
                    ControlButton.IsEnabled = true;
                    isWorking = false;
                });
            }

            UpdateBuildTitle();
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error during extraction: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Clipboard.SetText(ex.ToString());
            });
        }
    }

    private void UpdateBuildTitle()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                this.Title = "Lithicsoft AI Studio Installer | Build: " + File.ReadAllText(".build");
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating build title: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Clipboard.SetText(ex.ToString());
        }
    }

    private bool CheckForUpdates()
    {
        try
        {
            using var webClient = new WebClient();
            string fileContents = webClient.DownloadString(url);
            string[] lines = fileContents.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length > 0)
            {
                string latestBuild = lines[0];

                if (File.Exists(localFilePath))
                {
                    string localBuild = File.ReadAllText(localFilePath);
                    if (latestBuild == localBuild)
                    {
                        ShowNotification("Up-to-Date", "AI Studio is already up-to-date.");
                        return true;
                    }
                }

                ShowNotification("Update Available", "An update is available. Please update to the new version.");
                return false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Clipboard.SetText(ex.ToString());
        }

        return false;
    }

    private void CreateShortcut(string shortcutName, string targetFileLocation)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);

            string desktopShortcutLocation = Path.Combine(desktopPath, $"{shortcutName}.lnk");
            string startMenuShortcutLocation = Path.Combine(startMenuPath, $"{shortcutName}.lnk");

            WshShell shell = new();

            void ConfigureShortcut(string shortcutPath)
            {
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                shortcut.Description = "Shortcut for Lithicsoft AI Studio";
                shortcut.TargetPath = targetFileLocation;
                string? workingLocation = Path.GetDirectoryName(targetFileLocation);
                if (workingLocation != null)
                {
                    shortcut.WorkingDirectory = Path.GetFullPath(workingLocation);
                }
                shortcut.Save();
            }

            ConfigureShortcut(desktopShortcutLocation);
            ConfigureShortcut(startMenuShortcutLocation);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error creating shortcut: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Clipboard.SetText(ex.ToString());
            });
        }
    }

    private void ShowNotification(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (isWorking)
        {
            MessageBox.Show("You cannot close the installer right now!", "Installing AI Studio", MessageBoxButton.OK, MessageBoxImage.Stop);
            e.Cancel = true;
        }
        else if(MessageBox.Show("Do you want to exit Lithicsoft AI Studio Installer?", "Close Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
        {
            e.Cancel = true;       
        }
    }
}
