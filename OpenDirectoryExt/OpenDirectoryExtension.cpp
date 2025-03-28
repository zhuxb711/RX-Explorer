#include "pch.h"
#include "OpenDirectoryExtension.h"

HRESULT OpenDirectoryExtension::Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pBindContext*/)
{
    std::wstring cmdline = std::wstring(L"RX-Explorer.exe");

    if (psiItemArray) 
    {
        DWORD count;
        psiItemArray->GetCount(&count);

        for (DWORD index = 0; index < count; index++)
        {
            SFGAOF attributes;
            winrt::com_ptr<IShellItem> psi;

            RETURN_IF_FAILED(psiItemArray->GetItemAt(index, psi.put()));

            if ((psi->GetAttributes(SFGAO_FILESYSTEM, &attributes) == S_OK) && (psi->GetAttributes(SFGAO_FOLDER, &attributes) == S_OK))
            {
                wil::unique_cotaskmem_string pszName;
                RETURN_IF_FAILED(psi->GetDisplayName(SIGDN_FILESYSPATH, &pszName));

                cmdline += L" \"" + std::wstring(pszName.get()) + L"\"";
            }
        }
    }

    wil::unique_process_information _piClient;

    STARTUPINFOEX startInfoEx = STARTUPINFOEX();
    startInfoEx.StartupInfo.cb = sizeof(STARTUPINFOEX);

    RETURN_IF_WIN32_BOOL_FALSE(CreateProcessW(
        nullptr, //lpApplicationName
        cmdline.data(), //lpCommandLine
        nullptr, // lpProcessAttributes
        nullptr, // lpThreadAttributes
        false, // bInheritHandles
        EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, // dwCreationFlags
        nullptr, // lpEnvironment
        nullptr, //lpCurrentDictionary
        &startInfoEx.StartupInfo, // lpStartupInfo
        &_piClient // lpProcessInformation
    ));

    return S_OK;
}

HRESULT OpenDirectoryExtension::GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfoTip)
{
    return GetLocalizedString(L"/Resources/ContextMenu_DisplayName", ppszInfoTip);
}

HRESULT OpenDirectoryExtension::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    return GetLocalizedString(L"/Resources/ContextMenu_DisplayName", ppszName);
}

HRESULT OpenDirectoryExtension::GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pCmdState)
{
    *pCmdState = ECS_HIDDEN;

    if (winrt::unbox_value_or<bool>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"IntegrateWithWindowsExplorerContextMenu"), true))
    {
        *pCmdState = ECS_ENABLED;

        if (psiItemArray)
        {
            DWORD count;
            psiItemArray->GetCount(&count);

            for (DWORD index = 0; index < count; index++)
            {
                SFGAOF attributes;
                winrt::com_ptr<IShellItem> psi;

                psiItemArray->GetItemAt(index, psi.put());

                if ((psi->GetAttributes(SFGAO_FILESYSTEM, &attributes) != S_OK) || (psi->GetAttributes(SFGAO_FOLDER, &attributes) != S_OK))
                {
                    *pCmdState = ECS_HIDDEN;
                    break;
                }
            }
        }
    }

    return S_OK;
}

HRESULT OpenDirectoryExtension::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
	*ppszIcon = nullptr;
    winrt::hstring BasePath = winrt::Windows::ApplicationModel::Package::Current().InstalledPath();
    std::wstring LogoPath = std::wstring(BasePath.data(), BasePath.size()) + L"\\Assets\\AppLogo.png";
    return SHStrDupW(LogoPath.c_str(), ppszIcon);
}

HRESULT OpenDirectoryExtension::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

HRESULT OpenDirectoryExtension::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(this);
    return S_OK;
}

HRESULT OpenDirectoryExtension::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = nullptr;
    return E_NOTIMPL;
}

HRESULT OpenDirectoryExtension::GetLocalizedString(LPCWSTR resName, LPWSTR* ppszOutput)
{
    *ppszOutput = nullptr;

    auto candidate = ResourceManager::Current().MainResourceMap().TryLookup(resName);

    if (candidate)
    {
        auto resource = candidate.Resolve();

        if (resource)
        {
            return SHStrDupW(resource.ValueAsString().data(), ppszOutput);
        }
    }

    return E_FAIL;
}
