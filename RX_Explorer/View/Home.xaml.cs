using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
using TabViewItem = Microsoft.UI.Xaml.Controls.TabViewItem;

namespace RX_Explorer.View
{
    public sealed partial class Home : Page
    {
        public event EventHandler<string> EnterActionRequested;

        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayEnterCancellation;
        private CancellationTokenSource ContextMenuCancellation;
        private CommandBarFlyout LibraryFlyout;
        private CommandBarFlyout NormalDriveFlyout;
        private CommandBarFlyout PortableDriveFlyout;
        private CommandBarFlyout BitlockerDriveFlyout;

        public Home()
        {
            InitializeComponent();

            LibraryFlyout = CreateNewFolderContextMenu();
            NormalDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Normal);
            PortableDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Portable);
            BitlockerDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Locked);
        }

        private CommandBarFlyout CreateNewFolderContextMenu()
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Opening += LibraryFlyout_Opening;
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            #region PrimaryCommand -> StandBarContainer
            AppBarButton CopyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Copy }
            };
            ToolTipService.SetToolTip(CopyButton, Globalization.GetString("Operate_Text_Copy"));
            CopyButton.Click += Copy_Click;

            AppBarButton RenameButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Rename },
                Name = "RenameButton"
            };
            ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
            RenameButton.Click += LibraryRename_Click;

            AppBarButton RemovePinButton = new AppBarButton
            {
                Icon = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uE8D9" }
            };
            ToolTipService.SetToolTip(RemovePinButton, Globalization.GetString("Operate_Text_Unpin"));
            RemovePinButton.Click += RemovePin_Click;

            Flyout.PrimaryCommands.Add(CopyButton);
            Flyout.PrimaryCommands.Add(RenameButton);
            Flyout.PrimaryCommands.Add(RemovePinButton);
            #endregion

            #region SecondaryCommand -> OpenButton
            AppBarButton OpenButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                Label = Globalization.GetString("Operate_Text_Open"),
                Width = 320
            };
            OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            OpenButton.Click += OpenLibrary_Click;

            Flyout.SecondaryCommands.Add(OpenButton);
            #endregion

            #region SecondaryCommand -> OpenInNewTabButton
            AppBarButton OpenInNewTabButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_NewTab"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uF7ED"
                }
            };
            OpenInNewTabButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.T,
                IsEnabled = false
            });
            OpenInNewTabButton.Click += OpenInNewTab_Click;

            Flyout.SecondaryCommands.Add(OpenInNewTabButton);
            #endregion

            #region SecondaryCommand -> OpenInNewWindowButton
            AppBarButton OpenInNewWindowButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_NewWindow"),
                Width = 320,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uE727"
                }
            };
            OpenInNewWindowButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.Q,
                IsEnabled = false
            });
            OpenInNewWindowButton.Click += OpenInNewWindow_Click;

            Flyout.SecondaryCommands.Add(OpenInNewWindowButton);
            #endregion

            #region SecondaryCommand -> OpenInVerticalSplitViewButton
            AppBarButton OpenInVerticalSplitViewButton = new AppBarButton
            {
                Label = Globalization.GetString("Operate_Text_SplitView"),
                Width = 320,
                Name = "OpenInVerticalSplitView",
                Visibility = Visibility.Collapsed,
                Icon = new FontIcon
                {
                    FontFamily = FontIconFamily,
                    Glyph = "\uEA61"
                }
            };
            OpenInVerticalSplitViewButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Control,
                Key = VirtualKey.B,
                IsEnabled = false
            });
            OpenInVerticalSplitViewButton.Click += OpenInVerticalSplitView_Click;

            Flyout.SecondaryCommands.Add(OpenInVerticalSplitViewButton);
            #endregion

            Flyout.SecondaryCommands.Add(new AppBarSeparator());

            #region SecondaryCommand -> SendToButton
            AppBarButton SendToButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Send },
                Label = Globalization.GetString("SendTo/Label"),
                Width = 320
            };

            MenuFlyout SendToFlyout = new MenuFlyout();
            SendToFlyout.Opening += SendToFlyout_Opening;

            SendToButton.Flyout = SendToFlyout;

            Flyout.SecondaryCommands.Add(SendToButton);
            #endregion

            #region SecondaryCommand -> PropertyButton
            AppBarButton PropertyButton = new AppBarButton
            {
                Icon = new SymbolIcon { Symbol = Symbol.Tag },
                Width = 320,
                Label = Globalization.GetString("Operate_Text_Property")
            };
            PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Modifiers = VirtualKeyModifiers.Menu,
                Key = VirtualKey.Enter,
                IsEnabled = false
            });
            PropertyButton.Click += LibraryProperties_Click;

            Flyout.SecondaryCommands.Add(PropertyButton);
            #endregion

            return Flyout;
        }

        private CommandBarFlyout CreateNewDriveContextMenu(DriveContextMenuType Type)
        {
            CommandBarFlyout Flyout = new CommandBarFlyout
            {
                AlwaysExpanded = true,
                ShouldConstrainToRootBounds = false
            };
            Flyout.Closing += CommandBarFlyout_Closing;

            FontFamily FontIconFamily = Application.Current.Resources["SymbolThemeFontFamily"] as FontFamily;

            switch (Type)
            {
                case DriveContextMenuType.Portable:
                    {
                        #region SecondaryCommand -> EjectButton
                        AppBarButton EjectButton = new AppBarButton
                        {
                            Icon = new FontIcon
                            {
                                FontFamily = FontIconFamily,
                                Glyph = "\uF847"
                            },
                            Label = Globalization.GetString("Operate_Text_EjectUSB"),
                            Width = 320
                        };
                        EjectButton.Click += EjectButton_Click;

                        Flyout.SecondaryCommands.Add(EjectButton);
                        #endregion

                        goto case DriveContextMenuType.Normal;
                    }
                case DriveContextMenuType.Normal:
                    {
                        Flyout.Opening += DriveFlyout_Opening;

                        #region PrimaryCommand -> StandBarContainer
                        AppBarButton RenameButton = new AppBarButton
                        {
                            Icon = new SymbolIcon { Symbol = Symbol.Rename }
                        };
                        ToolTipService.SetToolTip(RenameButton, Globalization.GetString("Operate_Text_Rename"));
                        RenameButton.Click += DriveRename_Click;

                        Flyout.PrimaryCommands.Add(RenameButton);
                        #endregion

                        #region SecondaryCommand -> OpenButton
                        AppBarButton OpenButton = new AppBarButton
                        {
                            Icon = new SymbolIcon { Symbol = Symbol.OpenFile },
                            Label = Globalization.GetString("Operate_Text_Open"),
                            Width = 320
                        };
                        OpenButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Key = VirtualKey.Enter,
                            IsEnabled = false
                        });
                        OpenButton.Click += OpenDrive_Click;

                        Flyout.SecondaryCommands.Insert(0, OpenButton);
                        #endregion

                        #region SecondaryCommand -> OpenInNewTabButton
                        AppBarButton OpenInNewTabButton = new AppBarButton
                        {
                            Label = Globalization.GetString("Operate_Text_NewTab"),
                            Width = 320,
                            Icon = new FontIcon
                            {
                                FontFamily = FontIconFamily,
                                Glyph = "\uF7ED"
                            }
                        };
                        OpenInNewTabButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Modifiers = VirtualKeyModifiers.Control,
                            Key = VirtualKey.T,
                            IsEnabled = false
                        });
                        OpenInNewTabButton.Click += OpenInNewTab_Click;

                        Flyout.SecondaryCommands.Insert(1, OpenInNewTabButton);
                        #endregion

                        #region SecondaryCommand -> OpenInNewWindowButton
                        AppBarButton OpenInNewWindowButton = new AppBarButton
                        {
                            Label = Globalization.GetString("Operate_Text_NewWindow"),
                            Width = 320,
                            Icon = new FontIcon
                            {
                                FontFamily = FontIconFamily,
                                Glyph = "\uE727"
                            }
                        };
                        OpenInNewWindowButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Modifiers = VirtualKeyModifiers.Control,
                            Key = VirtualKey.Q,
                            IsEnabled = false
                        });
                        OpenInNewWindowButton.Click += OpenInNewWindow_Click;

                        Flyout.SecondaryCommands.Insert(2, OpenInNewWindowButton);
                        #endregion

                        #region SecondaryCommand -> OpenInVerticalSplitViewButton
                        AppBarButton OpenInVerticalSplitViewButton = new AppBarButton
                        {
                            Label = Globalization.GetString("Operate_Text_SplitView"),
                            Width = 320,
                            Name = "OpenInVerticalSplitView",
                            Visibility = Visibility.Collapsed,
                            Icon = new FontIcon
                            {
                                FontFamily = FontIconFamily,
                                Glyph = "\uEA61"
                            }
                        };
                        OpenInVerticalSplitViewButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Modifiers = VirtualKeyModifiers.Control,
                            Key = VirtualKey.B,
                            IsEnabled = false
                        });
                        OpenInVerticalSplitViewButton.Click += OpenInVerticalSplitView_Click;

                        Flyout.SecondaryCommands.Insert(3, OpenInVerticalSplitViewButton);
                        #endregion

                        Flyout.SecondaryCommands.Add(new AppBarSeparator());

                        #region SecondaryCommand -> PropertyButton
                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon { Symbol = Symbol.Tag },
                            Width = 320,
                            Label = Globalization.GetString("Operate_Text_Property")
                        };
                        PropertyButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Modifiers = VirtualKeyModifiers.Menu,
                            Key = VirtualKey.Enter,
                            IsEnabled = false
                        });
                        PropertyButton.Click += DriveProperties_Click;

                        Flyout.SecondaryCommands.Add(PropertyButton);
                        #endregion

                        break;
                    }
                case DriveContextMenuType.Locked:
                    {
                        #region SecondaryCommand -> UnlockBitlockerButton
                        AppBarButton UnlockBitlockerButton = new AppBarButton
                        {
                            Icon = new FontIcon
                            {
                                FontFamily = FontIconFamily,
                                Glyph = "\uE785"
                            },
                            Label = Globalization.GetString("Operate_Text_UnlockBitlocker"),
                            Width = 320
                        };
                        UnlockBitlockerButton.KeyboardAccelerators.Add(new KeyboardAccelerator
                        {
                            Modifiers = VirtualKeyModifiers.Control,
                            Key = VirtualKey.U,
                            IsEnabled = false
                        });
                        UnlockBitlockerButton.Click += UnlockBitlocker_Click;

                        Flyout.SecondaryCommands.Add(UnlockBitlockerButton);
                        #endregion

                        break;
                    }
                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            return Flyout;
        }

        private async void DriveFlyout_Opening(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                {
                    Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "OpenInVerticalSplitView").Visibility = Visibility.Visible;
                }
            }
        }

        private async void LibraryFlyout_Opening(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                if (LibraryGrid.SelectedItem is LibraryStorageFolder Folder)
                {
                    AppBarButton RenameButton = Flyout.PrimaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "RenameButton");

                    if (Folder.LibType == LibraryType.UserCustom)
                    {
                        RenameButton.IsEnabled = true;
                    }
                    else
                    {
                        RenameButton.IsEnabled = false;
                    }
                }

                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                {
                    Flyout.SecondaryCommands.OfType<AppBarButton>().First((Btn) => Btn.Name == "OpenInVerticalSplitView").Visibility = Visibility.Visible;
                }
            }
        }


        private async void DriveRename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (DriveGrid.SelectedItem is DriveDataBase RootDrive)
            {
                RenameDialog dialog = new RenameDialog(RootDrive);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    string OriginName = Regex.Replace(RootDrive.DisplayName, $@"\({Regex.Escape(RootDrive.Path.TrimEnd('\\'))}\)$", string.Empty).Trim();
                    string NewName = dialog.DesireNameMap[OriginName];

                    if (NewName != OriginName)
                    {
                        try
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                if (await Exclusive.Controller.SetDriveLabelAsync(RootDrive.Path, NewName))
                                {
                                    if (await DriveDataBase.CreateAsync(RootDrive) is DriveDataBase RefreshedDrive)
                                    {
                                        int Index = CommonAccessCollection.DriveList.IndexOf(RootDrive);

                                        if (CommonAccessCollection.DriveList.Remove(RootDrive))
                                        {
                                            CommonAccessCollection.DriveList.Insert(Index, RefreshedDrive);
                                        }
                                    }
                                    else
                                    {
                                        throw new UnauthorizedAccessException(RootDrive.Path);
                                    }
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not rename the drive");

                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await UnauthorizeDialog.ShowAsync();
                        }
                    }
                }
            }
        }

        private async void LibraryRename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Folder)
            {
                RenameDialog dialog = new RenameDialog(Folder);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    try
                    {
                        string OriginName = Folder.Name;
                        string OriginPath = Folder.Path;
                        string DesireName = dialog.DesireNameMap[OriginName];

                        if (OriginName != DesireName)
                        {
                            if (!OriginName.Equals(DesireName, StringComparison.OrdinalIgnoreCase)
                            && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(Path.GetDirectoryName(Folder.Path), DesireName)))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await Dialog.ShowAsync() != ContentDialogResult.Primary)
                                {
                                    return;
                                }
                            }

                            string NewName = await Folder.RenameAsync(DesireName);
                            string NewPath = Path.Combine(Path.GetDirectoryName(OriginPath), NewName);

                            if (await LibraryStorageFolder.CreateAsync(LibraryType.UserCustom, NewPath) is LibraryStorageFolder RefreshedLibrary)
                            {
                                int Index = CommonAccessCollection.LibraryList.IndexOf(RefreshedLibrary);

                                if (CommonAccessCollection.LibraryList.Remove(RefreshedLibrary))
                                {
                                    SQLite.Current.DeleteLibraryFolderRecord(OriginPath);
                                    SQLite.Current.SetLibraryPathRecord(LibraryType.UserCustom, NewPath);

                                    await JumpListController.Current.RemoveItemAsync(JumpListGroup.Library, OriginPath);
                                    await JumpListController.Current.AddItemAsync(JumpListGroup.Library, NewPath);

                                    CommonAccessCollection.LibraryList.Insert(Index, RefreshedLibrary);
                                }
                            }
                        }
                    }
                    catch (FileLoadException)
                    {
                        QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        await LoadExceptionDialog.ShowAsync();
                    }
                    catch (Exception)
                    {
                        QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await UnauthorizeDialog.ShowAsync();
                    }
                }
            }
        }

        private void CloseAllFlyout()
        {
            try
            {
                LibraryEmptyFlyout.Hide();
                DriveEmptyFlyout.Hide();
                NormalDriveFlyout.Hide();
                LibraryFlyout.Hide();
                PortableDriveFlyout.Hide();
                BitlockerDriveFlyout.Hide();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the flyout for unknown reason");
            }
        }

        private void DriveGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.AllowDrop = true;

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is DriveDataBase Drive)
                    {
                        await Drive.LoadAsync();

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            ProgressBar ProBar = e.ItemContainer.FindChildOfType<ProgressBar>();
                            Storyboard Story = new Storyboard();
                            DoubleAnimation Animation = new DoubleAnimation()
                            {
                                To = Drive.Percent,
                                From = 0,
                                EnableDependentAnimation = true,
                                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut },
                                Duration = new TimeSpan(0, 0, 0, 0, 800)
                            };
                            Storyboard.SetTarget(Animation, ProBar);
                            Storyboard.SetTargetProperty(Animation, "Value");
                            Story.Children.Add(Animation);
                            Story.Begin();
                        }
                    }
                });
            }
        }

        private void ItemContainer_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.AcceptedOperation = DataPackageOperation.None;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;

            if (e.DataView.Contains(StandardDataFormats.StorageItems)
                || e.DataView.Contains(ExtendedDataFormats.CompressionItems)
                || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem))
            {
                switch ((sender as SelectorItem)?.Content)
                {
                    case LibraryStorageFolder Folder:
                        {
                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.Name}\"";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.Name}\"";
                            }


                            break;
                        }
                    case DriveDataBase Drive when Drive is not LockedDriveData:
                        {
                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Drive.Name}\"";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Drive.Name}\"";
                            }

                            break;
                        }
                }
            }
        }

        private void ItemContainer_DragLeave(object sender, DragEventArgs e)
        {
            DelayEnterCancellation?.Cancel();
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is SelectorItem Selector)
            {
                DelayEnterCancellation?.Cancel();
                DelayEnterCancellation?.Dispose();
                DelayEnterCancellation = new CancellationTokenSource();

                Task.Delay(1500).ContinueWith((task, input) =>
                {
                    try
                    {
                        if (input is (SelectorItem Item, CancellationToken Token) && !Token.IsCancellationRequested)
                        {
                            switch (Item.Content)
                            {
                                case DriveDataBase Drive when Drive is not LockedDriveData:
                                    {
                                        EnterActionRequested?.Invoke(this, Drive.Path);
                                        break;
                                    }
                                case LibraryStorageFolder Lib when !LibraryGrid.CanReorderItems:
                                    {
                                        EnterActionRequested?.Invoke(this, Lib.Path);
                                        break;
                                    }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, (Selector, DelayEnterCancellation.Token), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void LibraryGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.AllowDrop = true;

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is LibraryStorageFolder Item)
                    {
                        await Item.LoadAsync().ConfigureAwait(false);
                    }
                });
            }
        }

        private async void ItemContainer_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                DelayEnterCancellation?.Cancel();

                IReadOnlyList<string> PathList = await e.DataView.GetAsStorageItemPathListAsync();

                if (PathList.Count > 0)
                {
                    switch ((sender as SelectorItem)?.Content)
                    {
                        case LibraryStorageFolder Lib:
                            {
                                if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                                {
                                    QueueTaskController.EnqueueMoveOpeartion(new OperationListMoveModel(PathList.ToArray(), Lib.Path));
                                }
                                else
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(PathList.ToArray(), Lib.Path));
                                }

                                break;
                            }
                        case DriveDataBase Drive when Drive is not LockedDriveData:
                            {
                                if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                                {
                                    QueueTaskController.EnqueueMoveOpeartion(new OperationListMoveModel(PathList.ToArray(), Drive.Path));
                                }
                                else
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(PathList.ToArray(), Drive.Path));
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                switch ((sender as SelectorItem)?.Content)
                {
                    case LibraryStorageFolder Lib:
                        {
                            QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(Lib.Path));
                            break;
                        }
                    case DriveDataBase Drive when Drive is not LockedDriveData:
                        {
                            QueueTaskController.EnqueueRemoteCopyOpeartion(new OperationListRemoteModel(Drive.Path));
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ItemContainer_Drop)}");

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ItemContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
            DelayEnterCancellation?.Cancel();
        }

        private void ItemContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is object Item)
            {
                switch (Item)
                {
                    case LibraryStorageFolder when !SettingPage.IsDoubleClickEnabled
                                                   && LibraryGrid.SelectedItem != Item
                                                   && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                                                   && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift):
                        {
                            DelaySelectionCancellation?.Cancel();
                            DelaySelectionCancellation?.Dispose();
                            DelaySelectionCancellation = new CancellationTokenSource();

                            Task.Delay(700).ContinueWith((task, input) =>
                            {
                                if (input is CancellationToken Token && !Token.IsCancellationRequested)
                                {
                                    LibraryGrid.SelectedItem = Item;
                                    DriveGrid.SelectedItem = null;
                                }
                            }, DelaySelectionCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());

                            break;
                        }
                    case DriveDataBase when !SettingPage.IsDoubleClickEnabled
                                            && LibraryGrid.SelectedItem != Item
                                            && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                                            && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift):
                        {
                            DelaySelectionCancellation?.Cancel();
                            DelaySelectionCancellation?.Dispose();
                            DelaySelectionCancellation = new CancellationTokenSource();

                            Task.Delay(700).ContinueWith((task, input) =>
                            {
                                if (input is CancellationToken Token && !Token.IsCancellationRequested)
                                {
                                    DriveGrid.SelectedItem = Item;
                                    LibraryGrid.SelectedItem = null;
                                }
                            }, DelaySelectionCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());

                            break;
                        }
                }
            }
        }

        public async Task OpenTargetDriveAsync(DriveDataBase Drive)
        {
            try
            {
                switch (Drive)
                {
                    case LockedDriveData LockedDrive:
                        {
                        Retry:
                            try
                            {
                                BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    if (!await LockedDrive.UnlockAsync(Dialog.Password))
                                    {
                                        throw new UnlockDriveFailedException();
                                    }

                                    if (await DriveDataBase.CreateAsync(LockedDrive) is DriveDataBase RefreshedDrive)
                                    {
                                        if (RefreshedDrive is LockedDriveData)
                                        {
                                            throw new UnlockDriveFailedException();
                                        }
                                        else
                                        {
                                            int Index = CommonAccessCollection.DriveList.IndexOf(LockedDrive);

                                            if (Index >= 0)
                                            {
                                                CommonAccessCollection.DriveList.Remove(LockedDrive);
                                                CommonAccessCollection.DriveList.Insert(Index, RefreshedDrive);
                                            }
                                            else
                                            {
                                                CommonAccessCollection.DriveList.Add(RefreshedDrive);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        throw new UnauthorizedAccessException(Drive.Path);
                                    }
                                }
                            }
                            catch (UnlockDriveFailedException)
                            {
                                QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnlockFailedDialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    return;
                                }
                            }

                            break;
                        }
                    case NormalDriveData:
                        {
                            OpenTargetFolder(string.IsNullOrEmpty(Drive.Path) ? Drive.DeviceId : Drive.Path);
                            break;
                        }
                }
            }
            catch (Exception)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} {Environment.NewLine}\"{Drive.Path}\"",
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                };

                await Dialog.ShowAsync();
            }
        }

        public void OpenTargetFolder(string Path)
        {
            DelaySelectionCancellation?.Cancel();
            EnterActionRequested?.Invoke(this, Path);
        }

        private async void DriveGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>()?.Content is DriveDataBase Drive)
                {
                    CoreWindow CWindow = CoreApplication.MainView.CoreWindow;

                    if (CWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Drive);
                        await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    }
                    else
                    {
                        await OpenTargetDriveAsync(Drive);
                    }
                }
            }
        }

        private async void LibraryGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>()?.Content is LibraryStorageFolder Library)
                {
                    CoreWindow CWindow = CoreApplication.MainView.CoreWindow;

                    if (CWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        if (Library.Path.Equals(Path.GetPathRoot(Library.Path), StringComparison.OrdinalIgnoreCase))
                        {
                            if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(Library.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive)
                            {
                                PropertiesWindowBase NewDriveWindow = await PropertiesWindowBase.CreateAsync(Drive);
                                await NewDriveWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                                return;
                            }
                            else if (Library.Path.StartsWith(@"\\"))
                            {
                                IReadOnlyList<DriveDataBase> NetworkDriveList = CommonAccessCollection.DriveList.Where((Drive) => Drive.DriveType == DriveType.Network).ToList();

                                if (NetworkDriveList.Count > 0)
                                {
                                    string RemappedPath = await UncPath.MapUncToDrivePath(NetworkDriveList.Select((Drive) => Drive.Path), Library.Path);

                                    if (NetworkDriveList.FirstOrDefault((Drive) => Drive.Path.Equals(RemappedPath, StringComparison.OrdinalIgnoreCase)) is DriveDataBase NetworkDrive)
                                    {
                                        PropertiesWindowBase NewNetworkDriveWindow = await PropertiesWindowBase.CreateAsync(NetworkDrive);
                                        await NewNetworkDriveWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                                    }
                                }
                            }
                        }

                        PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Library);
                        await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    }
                    else
                    {
                        OpenTargetFolder(Library.Path);
                    }
                }
            }
        }

        private async void OpenDrive_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            LibraryGrid.SelectedIndex = -1;

            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private void DriveGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                DriveGrid.SelectedIndex = -1;
            }
            else
            {
                LibraryGrid.SelectedIndex = -1;
            }
        }

        private void LibraryGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                LibraryGrid.SelectedIndex = -1;
            }
            else
            {
                DriveGrid.SelectedIndex = -1;
            }
        }

        private void Grid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;
            LibraryGrid.SelectedIndex = -1;
        }

        private void OpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            DriveGrid.SelectedIndex = -1;

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                OpenTargetFolder(Library.Path);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                CommonAccessCollection.LibraryList.Remove(Library);
                SQLite.Current.DeleteLibraryFolderRecord(Library.Path);
                await JumpListController.Current.RemoveItemAsync(JumpListGroup.Library, Library.Path);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                if (Library.Path.Equals(Path.GetPathRoot(Library.Path), StringComparison.OrdinalIgnoreCase))
                {
                    if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(Library.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive)
                    {
                        PropertiesWindowBase NewDriveWindow = await PropertiesWindowBase.CreateAsync(Drive);
                        await NewDriveWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                        return;
                    }
                    else if (Library.Path.StartsWith(@"\\"))
                    {
                        IReadOnlyList<DriveDataBase> NetworkDriveList = CommonAccessCollection.DriveList.Where((Drive) => Drive.DriveType == DriveType.Network).ToList();

                        if (NetworkDriveList.Count > 0)
                        {
                            string RemappedPath = await UncPath.MapUncToDrivePath(NetworkDriveList.Select((Drive) => Drive.Path), Library.Path);

                            if (NetworkDriveList.FirstOrDefault((Drive) => Drive.Path.Equals(RemappedPath, StringComparison.OrdinalIgnoreCase)) is DriveDataBase NetworkDrive)
                            {
                                PropertiesWindowBase NewDriveWindow = await PropertiesWindowBase.CreateAsync(NetworkDrive);
                                await NewDriveWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                                return;
                            }
                        }
                    }
                }

                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Library);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await CommonAccessCollection.LoadDriveAsync(true);
        }

        private async void DriveGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (!SettingPage.IsDoubleClickEnabled && e.ClickedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if (!SettingPage.IsDoubleClickEnabled && e.ClickedItem is LibraryStorageFolder Library)
            {
                OpenTargetFolder(Library.Path);
            }
        }

        private async void AddDrive_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                FolderPicker Picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                    ViewMode = PickerViewMode.Thumbnail
                };
                Picker.FileTypeFilter.Add("*");

                StorageFolder DriveFolder = await Picker.PickSingleFolderAsync();

                if (DriveFolder != null)
                {
                    if (DriveInfo.GetDrives().Where((Drive) => Drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.CDRom)
                                             .Any((Item) => Item.RootDirectory.FullName.Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (CommonAccessCollection.DriveList.Any((Item) => Item.Path.Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            LogTracer.Log("Could not add the drive to DriveList because it already exist in DriveList");

                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                Content = Globalization.GetString("QueueDialog_DeviceExist_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                        else
                        {
                            CommonAccessCollection.DriveList.Add(await DriveDataBase.CreateAsync(new DriveInfo(DriveFolder.Path)));
                        }
                    }
                    else
                    {
                        LogTracer.Log("Could not add the drive to the list because it is not in system drive list");

                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_DeviceSelectError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_TipTitle")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when adding to the drive list");
            }
        }

        private async void DriveProperties_Click(object sender, RoutedEventArgs e)
        {
            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                PropertiesWindowBase NewDriveWindow = await PropertiesWindowBase.CreateAsync(Drive);
                await NewDriveWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private void LibraryExpander_Collapsed(object sender, EventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;
        }

        private void DeviceExpander_Collapsed(object sender, EventArgs e)
        {
            DriveGrid.SelectedIndex = -1;
        }

        private async void EjectButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (DriveGrid.SelectedItem is DriveDataBase Item)
                {
                    if (string.IsNullOrEmpty(Item.Path))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (TabViewItem Tab in TabViewContainer.Current.TabCollection.ToArray())
                        {
                            if (Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                            {
                                if (Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)
                                                       .All((Path) => Item.Path.Equals(System.IO.Path.GetPathRoot(Path), StringComparison.OrdinalIgnoreCase)))
                                {
                                    await TabViewContainer.Current.CleanUpAndRemoveTabItem(Tab);
                                }
                                else
                                {
                                    foreach (FilePresenter Presenter in Renderer.Presenters.Where((Presenter) => Item.Path.Equals(Path.GetPathRoot(Presenter.CurrentFolder?.Path))))
                                    {
                                        await Renderer.CloseBladeByPresenterAsync(Presenter);
                                    }
                                }
                            }
                        }

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            if (await Exclusive.Controller.EjectPortableDevice(Item.Path))
                            {
                                ShowEjectNotification();
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog.ShowAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(EjectButton_Click)}");
            }
        }

        private void ShowEjectNotification()
        {
            try
            {
                ToastNotificationManager.History.Remove("MergeVideoNotification");

                ToastContentBuilder Builder = new ToastContentBuilder()
                                              .SetToastScenario(ToastScenario.Default)
                                              .AddToastActivationInfo("Transcode", ToastActivationType.Foreground)
                                              .AddText(Globalization.GetString("Eject_Toast_Text_1"))
                                              .AddText(Globalization.GetString("Eject_Toast_Text_2"))
                                              .AddText(Globalization.GetString("Eject_Toast_Text_3"));

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private void LibraryGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            SQLite.Current.ClearTable("Library");

            foreach (LibraryStorageFolder Item in CommonAccessCollection.LibraryList)
            {
                SQLite.Current.SetLibraryPathRecord(Item.LibType, Item.Path);
            }
        }

        private async void AddLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                if (CommonAccessCollection.LibraryList.Any((Library) => Folder.Path.Equals(Library?.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
                else if (!string.IsNullOrEmpty(Folder.Path))
                {
                    if (await LibraryStorageFolder.CreateAsync(LibraryType.UserCustom, Folder.Path) is LibraryStorageFolder LibFolder)
                    {
                        CommonAccessCollection.LibraryList.Add(LibFolder);
                        SQLite.Current.SetLibraryPathRecord(LibraryType.UserCustom, Folder.Path);
                        await JumpListController.Current.AddItemAsync(JumpListGroup.Library, Folder.Path);
                    }
                }
            }
        }

        private async void UnlockBitlocker_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private async void LibraryExpander_Expanded(object sender, EventArgs e)
        {
            await CommonAccessCollection.LoadLibraryFoldersAsync();
        }

        private async void DeviceExpander_Expanded(object sender, EventArgs e)
        {
            await CommonAccessCollection.LoadDriveAsync();
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    await Package.SetStorageItemDataAsync(LibraryGrid.SelectedItems.Cast<LibraryStorageFolder>().ToArray());

                    Clipboard.SetContent(Package);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void OpenInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                await TabViewContainer.Current.CreateNewTabAsync(Lib.Path);
            }
            else if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await TabViewContainer.Current.CreateNewTabAsync(Drive.Path);
            }
        }

        private async void OpenInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                {
                    new string[]{ Lib.Path }
                }));

                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{StartupArgument}"));
            }
            else if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                {
                    new string[]{ Drive.Path }
                }));

                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{StartupArgument}"));
            }
        }

        private async void OpenInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                await this.FindParentOfType<FileControl>()?.CreateNewBladeAsync(Lib.Path);
            }
            else if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await this.FindParentOfType<FileControl>()?.CreateNewBladeAsync(Drive.Path);
            }
        }

        private void SendToFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout Flyout)
            {
                foreach (MenuFlyoutItem Item in Flyout.Items)
                {
                    Item.Click -= SendToItem_Click;
                }

                Flyout.Items.Clear();

                MenuFlyoutItem SendDocumentItem = new MenuFlyoutItem
                {
                    Name = "SendDocumentItem",
                    Text = Globalization.GetString("SendTo_Document"),
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/DocumentIcon.ico"))
                    },
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendDocumentItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendDocumentItem);

                MenuFlyoutItem SendLinkItem = new MenuFlyoutItem
                {
                    Name = "SendLinkItem",
                    Text = Globalization.GetString("SendTo_CreateDesktopShortcut"),
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/DesktopIcon.ico"))
                    },
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendLinkItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendLinkItem);

                foreach (DriveDataBase RemovableDrive in CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType is DriveType.Removable or DriveType.Network) && !string.IsNullOrEmpty(Drive.Path)).ToArray())
                {
                    MenuFlyoutItem SendRemovableDriveItem = new MenuFlyoutItem
                    {
                        Name = "SendRemovableItem",
                        Text = $"{(string.IsNullOrEmpty(RemovableDrive.DisplayName) ? RemovableDrive.Path : RemovableDrive.DisplayName)}",
                        Icon = new ImageIcon
                        {
                            Source = RemovableDrive.Thumbnail
                        },
                        MinWidth = 150,
                        MaxWidth = 350,
                        Tag = RemovableDrive.Path
                    };
                    SendRemovableDriveItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendRemovableDriveItem);
                }
            }
        }

        private void CommandBarFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                foreach (AppBarButton Btn in Flyout.SecondaryCommands.OfType<AppBarButton>())
                {
                    Btn.Flyout?.Hide();
                }
            }
        }

        private async void SendToItem_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (sender is FrameworkElement Item)
                {
                    if (LibraryGrid.SelectedItem is LibraryStorageFolder SItem)
                    {
                        switch (Item.Name)
                        {
                            case "SendLinkItem":
                                {
                                    string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                                    if (await FileSystemStorageItemBase.CheckExistsAsync(DesktopPath))
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                        {
                                            if (!await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                            {
                                                LinkPath = Path.Combine(DesktopPath, $"{SItem.Name}.lnk"),
                                                LinkTargetPath = SItem.Path
                                            }))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            IReadOnlyList<User> UserList = await User.FindAllAsync();

                                            UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                     ? UserDataPaths.GetForUser(CurrentUser)
                                                                     : UserDataPaths.GetDefault();

                                            if (await FileSystemStorageItemBase.CheckExistsAsync(DataPath.Desktop))
                                            {
                                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                {
                                                    if (!await Exclusive.Controller.CreateLinkAsync(new LinkFileData
                                                    {
                                                        LinkPath = Path.Combine(DataPath.Desktop, $"{SItem.Name}.lnk"),
                                                        LinkTargetPath = SItem.Path
                                                    }))
                                                    {
                                                        QueueContentDialog Dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                LogTracer.Log($"Could not execute \"Send to\" command because desktop path \"{DataPath.Desktop}\" is not exists");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not get desktop path from UserDataPaths");
                                        }
                                    }

                                    break;
                                }
                            case "SendDocumentItem":
                                {
                                    string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                                    if (await FileSystemStorageItemBase.CheckExistsAsync(DocumentPath))
                                    {
                                        QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, DocumentPath));
                                    }
                                    else
                                    {
                                        try
                                        {
                                            IReadOnlyList<User> UserList = await User.FindAllAsync();

                                            UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                     ? UserDataPaths.GetForUser(CurrentUser)
                                                                     : UserDataPaths.GetDefault();

                                            if (await FileSystemStorageItemBase.CheckExistsAsync(DataPath.Documents))
                                            {
                                                QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, DataPath.Documents));
                                            }
                                            else
                                            {
                                                LogTracer.Log($"Could not execute \"Send to\" command because document path \"{DataPath.Documents}\" is not exists");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not get document path from UserDataPaths");
                                        }
                                    }

                                    break;
                                }
                            case "SendRemovableItem":
                                {
                                    if (Item.Tag is string RemovablePath)
                                    {
                                        QueueTaskController.EnqueueCopyOpeartion(new OperationListCopyModel(new string[] { SItem.Path }, RemovablePath));
                                    }

                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(SendToItem_Click)}");
            }
        }

        private void LibraryGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            LibraryGrid.CanReorderItems = LibraryGrid.IsDragSource();
        }

        private async void LibraryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingPage.IsQuicklookEnabled
                && !SettingPage.IsOpened
                && e.AddedItems.Count == 1
                && e.AddedItems.First() is LibraryStorageFolder Item)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                        {
                            if (!string.IsNullOrEmpty(Item.Path))
                            {
                                await Exclusive.Controller.SwitchQuicklookAsync(Item.Path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(LibraryGrid_SelectionChanged)}");
                }
            }
        }

        private async void DriveGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingPage.IsQuicklookEnabled
                && !SettingPage.IsOpened
                && e.AddedItems.Count == 1
                && e.AddedItems.First() is DriveDataBase Item)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                        {
                            if (!string.IsNullOrEmpty(Item.Path))
                            {
                                await Exclusive.Controller.SwitchQuicklookAsync(Item.Path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(DriveGrid_SelectionChanged)}");
                }
            }
        }

        private async void LibraryGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point Position))
            {
                args.Handled = true;

                if (!SettingPage.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((args.OriginalSource as FrameworkElement)?.DataContext is LibraryStorageFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;

                    ContextMenuCancellation?.Cancel();
                    ContextMenuCancellation?.Dispose();
                    ContextMenuCancellation = new CancellationTokenSource();

                    for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                    {
                        try
                        {
                            await LibraryFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(LibraryGrid,
                                                                                              Position,
                                                                                              ContextMenuCancellation.Token,
                                                                                              Context.Path);
                            break;
                        }
                        catch (Exception)
                        {
                            LibraryFlyout = CreateNewFolderContextMenu();
                        }
                    }
                }
                else
                {
                    LibraryGrid.SelectedIndex = -1;

                    LibraryEmptyFlyout.ShowAt(LibraryGrid, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
            }
        }

        private void LibraryGrid_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }

        private async void DriveGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point Position))
            {
                if (!SettingPage.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((args.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Context)
                {
                    DriveGrid.SelectedItem = Context;

                    ContextMenuCancellation?.Cancel();
                    ContextMenuCancellation?.Dispose();
                    ContextMenuCancellation = new CancellationTokenSource();

                    switch (Context)
                    {
                        case LockedDriveData:
                            {
                                for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                {
                                    try
                                    {
                                        await BitlockerDriveFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(DriveGrid,
                                                                                                                 Position,
                                                                                                                 ContextMenuCancellation.Token,
                                                                                                                 Context.Path);
                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        BitlockerDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Locked);
                                    }
                                }

                                break;
                            }
                        case NormalDriveData when Context.DriveType == DriveType.Removable:
                            {
                                for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                {
                                    try
                                    {
                                        await PortableDriveFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(DriveGrid,
                                                                                                                Position,
                                                                                                                ContextMenuCancellation.Token,
                                                                                                                Context.Path);
                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        PortableDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Portable);
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                for (int RetryCount = 0; RetryCount < 3; RetryCount++)
                                {
                                    try
                                    {
                                        await NormalDriveFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(DriveGrid,
                                                                                                              Position,
                                                                                                              ContextMenuCancellation.Token,
                                                                                                              Context.Path);
                                        break;
                                    }
                                    catch (Exception)
                                    {
                                        NormalDriveFlyout = CreateNewDriveContextMenu(DriveContextMenuType.Normal);
                                    }
                                }
                            }

                            break;
                    }
                }
                else
                {
                    DriveGrid.SelectedIndex = -1;

                    DriveEmptyFlyout.ShowAt(DriveGrid, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
            }
        }

        private void DriveGrid_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }
    }
}
