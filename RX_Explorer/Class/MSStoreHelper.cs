using System;
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

        public static MSStoreHelper Current => Instance ??= new MSStoreHelper();

        public async Task<bool> CheckPurchaseStatusAsync()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("LicenseGrant", out object GrantState) && Convert.ToBoolean(GrantState))
                {
                    return true;
                }

                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                await PreLoadTask;

                if (License != null)
                {
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
                else
                {
                    return false;
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
                if (PreLoadTask == null)
                {
                    PreLoadStoreData();
                }

                await PreLoadTask;

                if (ProductResult != null && ProductResult.ExtendedError == null)
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

        public void PreLoadStoreData()
        {
            PreLoadTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    Store = StoreContext.GetDefault();
                    Store.OfflineLicensesChanged += Store_OfflineLicensesChanged;

                    License = Store.GetAppLicenseAsync().AsTask().Result;
                    ProductResult = Store.GetStoreProductForCurrentAppAsync().AsTask().Result;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not load MSStore data");
                }
            }, TaskCreationOptions.LongRunning);
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(Store_OfflineLicensesChanged)} threw an exception");
            }
        }
    }
}
