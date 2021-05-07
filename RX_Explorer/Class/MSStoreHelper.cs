using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class MSStoreHelper
    {
        private static MSStoreHelper Instance;

        private StoreContext Store;

        private Task<StoreAppLicense> GetLicenseTask;

        private readonly ManualResetEvent InitLocker;

        public static MSStoreHelper Current => Instance ??= new MSStoreHelper();

        public async Task<bool> CheckPurchaseStatusAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    InitLocker.WaitOne();
                });

                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("LicenseGrant", out object GrantState) && Convert.ToBoolean(GrantState))
                {
                    return true;
                }

                StoreAppLicense License = GetLicenseTask == null ? await Store.GetAppLicenseAsync() : await GetLicenseTask.ConfigureAwait(false);

                if (License.AddOnLicenses.Any((Item) => Item.Value.InAppOfferToken == "Donation"))
                {
                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                    return true;
                }
                else
                {
                    if (License.IsActive)
                    {
                        if (License.IsTrial)
                        {
                            ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                            return false;
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                            return true;
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = false;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckPurchaseStatusAsync)} threw an exception");
                return false;
            }
        }

        public async Task<StorePurchaseStatus> PurchaseAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    InitLocker.WaitOne();
                });

                StoreProductResult ProductResult = await Store.GetStoreProductForCurrentAppAsync();

                if (ProductResult.ExtendedError == null)
                {
                    if (ProductResult.Product != null)
                    {
                        StorePurchaseResult Result = await ProductResult.Product.RequestPurchaseAsync();

                        switch (Result.Status)
                        {
                            case StorePurchaseStatus.AlreadyPurchased:
                            case StorePurchaseStatus.Succeeded:
                                {
                                    ApplicationData.Current.LocalSettings.Values["LicenseGrant"] = true;
                                    break;
                                }
                        }

                        return Result.Status;
                    }
                    else
                    {
                        return StorePurchaseStatus.NetworkError;
                    }
                }
                else
                {
                    return StorePurchaseStatus.NetworkError;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(PurchaseAsync)} threw an exception");
                return StorePurchaseStatus.NetworkError;
            }
        }

        private void PreLoadAppLicense()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("LicenseGrant", out object GrantState))
            {
                if (!Convert.ToBoolean(GrantState))
                {
                    GetLicenseTask = Store.GetAppLicenseAsync().AsTask();
                }
            }
            else
            {
                GetLicenseTask = Store.GetAppLicenseAsync().AsTask();
            }
        }

        public Task InitializeAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                Store = StoreContext.GetDefault();
                Store.OfflineLicensesChanged += Store_OfflineLicensesChanged;

                PreLoadAppLicense();

                InitLocker.Set();
            }, TaskCreationOptions.LongRunning);
        }

        private MSStoreHelper()
        {
            InitLocker = new ManualResetEvent(false);
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Store_OfflineLicensesChanged)} threw an exception");
            }
        }
    }
}
