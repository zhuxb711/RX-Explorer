using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class MSStoreHelper
    {
        private static MSStoreHelper Instance;

        private StoreContext Store;
        private StoreAppLicense License;
        private StoreProductResult ProductResult;
        private Task PreLoadTask;
        private Task<bool> CheckPurchaseStatusTask;
        private Task<bool> CheckHasUpdate;
        private Task<bool> CheckIfUpdateIsMandatory;
        private IReadOnlyList<StorePackageUpdate> Updates;
        private static readonly object Locker = new object();

        public static MSStoreHelper Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new MSStoreHelper();
                }
            }
        }

        public Task<bool> CheckPurchaseStatusAsync()
        {
#if DEBUG
            return Task.FromResult(true);
#else
            if (ApplicationData.Current.LocalSettings.Values["LicenseGrant"] is bool IsGrant && IsGrant)
            {
                if (SystemInformation.Instance.TotalLaunchCount % 5 > 0)
                {
                    return Task.FromResult(true);
                }
            }

            lock (Locker)
            {
                return CheckPurchaseStatusTask ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        if (License != null)
                        {
                            if ((License.IsActive && !License.IsTrial) || (License.AddOnLicenses?.Any((Item) => Item.Value.InAppOfferToken == "Donation")).GetValueOrDefault())
                            {
                                ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                return true;
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckPurchaseStatusAsync)} threw an exception");
                    }

                    return false;
                });
            }
#endif
        }

        public Task<bool> CheckHasUpdateAsync()
        {
            lock (Locker)
            {
                return CheckHasUpdate ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        if (Updates != null)
                        {
                            return Updates.Any();
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckHasUpdateAsync)} threw an exception");
                        return false;
                    }
                });
            }
        }

        public Task<bool> CheckIfUpdateIsMandatoryAsync()
        {
            lock (Locker)
            {
                return CheckIfUpdateIsMandatory ??= PreLoadStoreData().ContinueWith((_) =>
                {
                    try
                    {
                        if (Updates != null)
                        {
                            foreach (StorePackageUpdate Update in Updates)
                            {
                                if (Update.Mandatory)
                                {
                                    return true;
                                }
                            }

                            return false;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"{nameof(CheckIfUpdateIsMandatoryAsync)} threw an exception");
                        return false;
                    }
                });
            }
        }

        public Task<StorePurchaseStatus> PurchaseAsync()
        {
            return PreLoadStoreData().ContinueWith((_) =>
            {
                try
                {
                    if (ProductResult != null && ProductResult.ExtendedError == null)
                    {
                        if (ProductResult.Product != null)
                        {
                            StorePurchaseResult Result = ProductResult.Product.RequestPurchaseAsync().AsTask().Result;

                            switch (Result.Status)
                            {
                                case StorePurchaseStatus.AlreadyPurchased:
                                case StorePurchaseStatus.Succeeded:
                                    {
                                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                        break;
                                    }
                                default:
                                    {
                                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                                        break;
                                    }
                            }

                            return Result.Status;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(PurchaseAsync)} threw an exception");
                }

                return StorePurchaseStatus.NetworkError;
            });
        }

        public Task PreLoadStoreData()
        {
            lock (Locker)
            {
                return PreLoadTask ??= Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Store = StoreContext.GetDefault();
                        Store.OfflineLicensesChanged += Store_OfflineLicensesChanged;

                        License = Store.GetAppLicenseAsync().AsTask().Result;
                        ProductResult = Store.GetStoreProductForCurrentAppAsync().AsTask().Result;

#if DEBUG
                        Updates = new List<StorePackageUpdate>(0);
#else
                        if (Windows.ApplicationModel.Package.Current.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store)
                        {
                            Updates = Store.GetAppAndOptionalStorePackageUpdatesAsync().AsTask().Result;
                        }
#endif
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not load MSStore data");
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }

        private MSStoreHelper()
        {

        }

        private async void Store_OfflineLicensesChanged(StoreContext sender, object args)
        {
            try
            {
                StoreAppLicense License = await sender.GetAppLicenseAsync();

                if (License.IsActive && !License.IsTrial)
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Store_OfflineLicensesChanged)} threw an exception");
            }
        }
    }
}
