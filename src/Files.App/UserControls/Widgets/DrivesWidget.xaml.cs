using Files.App.DataModels.NavigationControlItems;
using Files.App.Helpers;
using Files.Backend.Services.Settings;
using Files.App.ViewModels.Widgets;
using Files.App.Extensions;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.WinUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Specialized;

namespace Files.App.UserControls.Widgets
{
    public class DriveCardItem : ObservableObject, IWidgetCardItem<DriveItem>
    {
        private BitmapImage thumbnail;
        private byte[] thumbnailData;

        public DriveItem Item { get; private set; }
        public bool HasThumbnail => thumbnail != null && thumbnailData != null;
        public BitmapImage Thumbnail
        {
            get => thumbnail;
            set => SetProperty(ref thumbnail, value);
        }

        public DriveCardItem(DriveItem item)
        {
            this.Item = item;
        }

        public async Task LoadCardThumbnailAsync(int overrideThumbnailSize = 32)
        {
            if (thumbnailData == null || thumbnailData.Length == 0)
            {
                // Try load thumbnail using ListView mode
                thumbnailData = await FileThumbnailHelper.LoadIconFromPathAsync(Item.Path, Convert.ToUInt32(overrideThumbnailSize), Windows.Storage.FileProperties.ThumbnailMode.ListView);
            }
            if (thumbnailData == null || thumbnailData.Length == 0)
            {
                // Thumbnail is still null, use DriveItem icon (loaded using SingleItem mode)
                thumbnailData = Item.IconData;
            }
            if (thumbnailData != null && thumbnailData.Length > 0)
            {
                // Thumbnail data is valid, set the item icon
                Thumbnail = await App.Window.DispatcherQueue.EnqueueAsync(() => thumbnailData.ToBitmapAsync(overrideThumbnailSize));
            }
        }
    }

    public sealed partial class DrivesWidget : UserControl, IWidgetItemModel, INotifyPropertyChanged
    {
        private IUserSettingsService UserSettingsService { get; } = Ioc.Default.GetService<IUserSettingsService>();

        public delegate void DrivesWidgetInvokedEventHandler(object sender, DrivesWidgetInvokedEventArgs e);

        public event DrivesWidgetInvokedEventHandler DrivesWidgetInvoked;

        public delegate void DrivesWidgetNewPaneInvokedEventHandler(object sender, DrivesWidgetInvokedEventArgs e);

        public event DrivesWidgetNewPaneInvokedEventHandler DrivesWidgetNewPaneInvoked;

        public event PropertyChangedEventHandler PropertyChanged;

        public static ObservableCollection<DriveCardItem> ItemsAdded = new ObservableCollection<DriveCardItem>();

        private IShellPage associatedInstance;

        public IShellPage AppInstance
        {
            get => associatedInstance;
            set
            {
                if (value != associatedInstance)
                {
                    associatedInstance = value;
                    NotifyPropertyChanged(nameof(AppInstance));
                }
            }
        }

        public string WidgetName => nameof(DrivesWidget);

        public string AutomationProperties => "DrivesWidgetAutomationProperties/Name".GetLocalizedResource();

        public string WidgetHeader => "Drives".GetLocalizedResource();

        public bool IsWidgetSettingEnabled => UserSettingsService.WidgetsSettingsService.ShowDrivesWidget;

        public DrivesWidget()
        {
            InitializeComponent();

            Manager_DataChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            App.DrivesManager.DataChanged += Manager_DataChanged;
        }

        private async void Manager_DataChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await DispatcherQueue.EnqueueAsync(async () =>
            {
                foreach (DriveItem drive in App.DrivesManager.Drives)
                {
                    if (!ItemsAdded.Any(x => x.Item == drive))
                    {
                        if (drive.Type != DriveType.VirtualDrive)
                        {
                            var cardItem = new DriveCardItem(drive);
                            ItemsAdded.Add(cardItem);
                            await cardItem.LoadCardThumbnailAsync(); // After add
                        }
                    }
                }

                foreach (DriveCardItem driveCard in ItemsAdded.ToList())
                {
                    if (!App.DrivesManager.Drives.Contains(driveCard.Item))
                    {
                        ItemsAdded.Remove(driveCard);
                    }
                }
            });
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void EjectDevice_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            await DriveHelpers.EjectDeviceAsync(item.Path);
        }

        private async void OpenInNewTab_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            if (await CheckEmptyDrive(item.Path))
            {
                return;
            }
            await NavigationHelpers.OpenPathInNewTab(item.Path);
        }

        private async void OpenInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            if (await CheckEmptyDrive(item.Path))
            {
                return;
            }
            await NavigationHelpers.OpenPathInNewWindowAsync(item.Path);
        }

        private async void PinToFavorites_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            if (await CheckEmptyDrive(item.Path))
            {
                return;
            }
            App.SidebarPinnedController.Model.AddItem(item.Path);
        }

        private async void UnpinFromFavorites_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            if (await CheckEmptyDrive(item.Path))
            {
                return;
            }
            App.SidebarPinnedController.Model.RemoveItem(item.Path);
        }

        private async void OpenDriveProperties_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            await FilePropertiesHelpers.OpenPropertiesWindowAsync(item, associatedInstance);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string ClickedCard = (sender as Button).Tag.ToString();
            string NavigationPath = ClickedCard; // path to navigate

            if (await CheckEmptyDrive(NavigationPath))
            {
                return;
            }

            var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (ctrlPressed)
            {
                await NavigationHelpers.OpenPathInNewTab(NavigationPath);
                return;
            }

            DrivesWidgetInvoked?.Invoke(this, new DrivesWidgetInvokedEventArgs()
            {
                Path = NavigationPath
            });
        }

        private async void Button_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed) // check middle click
            {
                string navigationPath = (sender as Button).Tag.ToString();
                if (await CheckEmptyDrive(navigationPath))
                {
                    return;
                }
                await NavigationHelpers.OpenPathInNewTab(navigationPath);
            }
        }

        public class DrivesWidgetInvokedEventArgs : EventArgs
        {
            public string Path { get; set; }
        }

        public bool ShowMultiPaneControls
        {
            get => AppInstance.PaneHolder?.IsMultiPaneEnabled ?? false;
        }

        private async void OpenInNewPane_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            if (await CheckEmptyDrive(item.Path))
            {
                return;
            }
            DrivesWidgetNewPaneInvoked?.Invoke(this, new DrivesWidgetInvokedEventArgs()
            {
                Path = item.Path
            });
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var newPaneMenuItem = (sender as MenuFlyout).Items.Single(x => x.Name == "OpenInNewPane");
            newPaneMenuItem.Visibility = ShowMultiPaneControls ? Visibility.Visible : Visibility.Collapsed;

            var pinToFavoritesItem = (sender as MenuFlyout).Items.Single(x => x.Name == "PinToFavorites");
            pinToFavoritesItem.Visibility = (pinToFavoritesItem.DataContext as DriveItem).IsPinned ? Visibility.Collapsed : Visibility.Visible;

            var unpinFromFavoritesItem = (sender as MenuFlyout).Items.Single(x => x.Name == "UnpinFromFavorites");
            unpinFromFavoritesItem.Visibility = (unpinFromFavoritesItem.DataContext as DriveItem).IsPinned ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void MapNetworkDrive_Click(object sender, RoutedEventArgs e)
        {
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection != null)
            {
                await connection.SendMessageAsync(new ValueSet()
                {
                    { "Arguments", "NetworkDriveOperation" },
                    { "netdriveop", "OpenMapNetworkDriveDialog" },
                    { "HWND", NativeWinApiHelper.CoreWindowHandle.ToInt64() }
                });
            }
        }

        private async void DisconnectNetworkDrive_Click(object sender, RoutedEventArgs e)
        {
            var item = ((MenuFlyoutItem)sender).DataContext as DriveItem;
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection != null)
            {
                await connection.SendMessageAsync(new ValueSet()
                {
                    { "Arguments", "NetworkDriveOperation" },
                    { "netdriveop", "DisconnectNetworkDrive" },
                    { "drive", item.Path }
                });
            }
        }

        private void GoToStorageSense_Click(object sender, RoutedEventArgs e)
        {
            string clickedCard = (sender as Button).Tag.ToString();
            StorageSenseHelper.OpenStorageSense(clickedCard);
        }

        private async Task<bool> CheckEmptyDrive(string drivePath)
        {
            if (drivePath is not null)
            {
                var matchingDrive = App.DrivesManager.Drives.FirstOrDefault(x => drivePath.StartsWith(x.Path, StringComparison.Ordinal));
                if (matchingDrive != null && matchingDrive.Type == DriveType.CDRom && matchingDrive.MaxSpace == ByteSizeLib.ByteSize.FromBytes(0))
                {
                    bool ejectButton = await DialogDisplayHelper.ShowDialogAsync("InsertDiscDialog/Title".GetLocalizedResource(), string.Format("InsertDiscDialog/Text".GetLocalizedResource(), matchingDrive.Path), "InsertDiscDialog/OpenDriveButton".GetLocalizedResource(), "Close".GetLocalizedResource());
                    if (ejectButton)
                    {
                        await DriveHelpers.EjectDeviceAsync(matchingDrive.Path);
                    }
                    return true;
                }
            }
            return false;
        }

        public async Task RefreshWidget()
        {
            foreach (var item in ItemsAdded)
            {
                await item.Item.UpdatePropertiesAsync();
            }
        }

        public void Dispose()
        {

        }
    }
}
