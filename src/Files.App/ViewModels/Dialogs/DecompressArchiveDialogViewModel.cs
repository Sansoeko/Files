using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Files.App.ViewModels.Dialogs
{
    public class DecompressArchiveDialogViewModel : ObservableObject
    {
        private readonly IStorageFile archive;

        public StorageFolder DestinationFolder { get; private set; }

        private string destinationFolderPath;

        public string DestinationFolderPath
        {
            get => destinationFolderPath;
            private set => SetProperty(ref destinationFolderPath, value);
        }

        private bool openDestinationFolderOnCompletion;

        public bool OpenDestinationFolderOnCompletion
        {
            get => openDestinationFolderOnCompletion;
            set => SetProperty(ref openDestinationFolderOnCompletion, value);
        }

        public ICommand SelectDestinationCommand { get; private set; }

        public DecompressArchiveDialogViewModel(IStorageFile archive)
        {
            this.archive = archive;
            this.destinationFolderPath = DefaultDestinationFolderPath();

            // Create commands
            SelectDestinationCommand = new AsyncRelayCommand(SelectDestination);
        }

        private async Task SelectDestination()
        {
            FolderPicker folderPicker = this.InitializeWithWindow(new FolderPicker());
            folderPicker.FileTypeFilter.Add("*");

            DestinationFolder = await folderPicker.PickSingleFolderAsync();

            if (DestinationFolder != null)
            {
                DestinationFolderPath = DestinationFolder.Path;
            }
            else
            {
                DestinationFolderPath = DefaultDestinationFolderPath();
            }
        }

        // WINUI3
        private FolderPicker InitializeWithWindow(FolderPicker obj)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(obj, App.WindowHandle);
            return obj;
        }

        private string DefaultDestinationFolderPath()
        {
            return Path.Combine(Path.GetDirectoryName(archive.Path), Path.GetFileNameWithoutExtension(archive.Path));
        }
    }
}