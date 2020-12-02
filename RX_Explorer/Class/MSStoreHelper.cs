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

        private readonly StoreContext Store;

        public static MSStoreHelper Current => Instance ??= new MSStoreHelper();

        public async Task<bool> CheckPurchaseStatusAsync()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("LicenseGrant"))
                {
                    return true;
                }

                StoreAppLicense License = await Store.GetAppLicenseAsync();

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
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<StorePurchaseStatus> PurchaseAsync()
        {
            try
            {
                StoreProductResult ProductResult = await Store.GetStoreProductForCurrentAppAsync();

                if (ProductResult.ExtendedError == null)
                {
                    if (ProductResult.Product != null)
                    {
                        StorePurchaseResult Result = await ProductResult.Product.RequestPurchaseAsync();

                        switch(Result.Status)
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
            catch
            {
                return StorePurchaseStatus.NetworkError;
            }
        }

        private MSStoreHelper()
        {
            Store = StoreContext.GetDefault();
        }
    }
}
