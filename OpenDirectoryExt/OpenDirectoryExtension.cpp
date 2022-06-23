#include "pch.h"
#include "OpenDirectoryExtension.h"
#include <wil/filesystem.h>
#include <winrt\Windows.Storage.h>
#include <winrt\Windows.Foundation.Collections.h>

static constexpr std::wstring_view VerbName{ L"RxExplorerOpenHere" };
static constexpr std::wstring_view DefaultDisplayName{ L"Open in RX-Explorer" };

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Method Description:
// - This method is called when the user activates the context menu item. We'll
//   launch the Terminal using the current working directory.
// Arguments:
// - psiItemArray: a IShellItemArray which contains the item that's selected.
// Return Value:
// - S_OK if we successfully attempted to launch the Terminal, otherwise a
//   failure from an earlier HRESULT.
HRESULT OpenTerminalHere::Invoke(IShellItemArray* psiItemArray,
    IBindCtx* /*pBindContext*/)
{
    std::wstring cmdline = std::wstring(L"RX-Explorer.exe");

    if (psiItemArray != nullptr) 
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

HRESULT OpenTerminalHere::GetToolTip(IShellItemArray* /*psiItemArray*/,
    LPWSTR* ppszInfoTip)
{
    winrt::hstring DisplayName = winrt::unbox_value_or<winrt::hstring>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"GlobalizationStringForContextMenu"), DefaultDisplayName);
    return SHStrDupW(DisplayName.c_str(), ppszInfoTip);
}

HRESULT OpenTerminalHere::GetTitle(IShellItemArray* /*psiItemArray*/,
    LPWSTR* ppszName)
{
    winrt::hstring DisplayName = winrt::unbox_value_or<winrt::hstring>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"GlobalizationStringForContextMenu"), DefaultDisplayName);
    return SHStrDupW(DisplayName.c_str(), ppszName);
}

HRESULT OpenTerminalHere::GetState(IShellItemArray* psiItemArray,
    BOOL /*fOkToBeSlow*/,
    EXPCMDSTATE* pCmdState)
{
    // compute the visibility of the verb here, respect "fOkToBeSlow" if this is
    // slow (does IO for example) when called with fOkToBeSlow == FALSE return
    // E_PENDING and this object will be called back on a background thread with
    // fOkToBeSlow == TRUE

    // We however don't need to bother with any of that, so we'll just return
    // ECS_ENABLED.
    if (psiItemArray == nullptr) 
    {
        *pCmdState = ECS_HIDDEN;
    }
    else
    {
        *pCmdState = ECS_ENABLED;

        if (winrt::unbox_value_or<bool>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"IntegrateWithWindowsExplorerContextMenu"), true))
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
        else 
        {
            *pCmdState = ECS_HIDDEN;
        }
    }

    return S_OK;
}

STDMETHODIMP_(HRESULT __stdcall) OpenTerminalHere::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    winrt::hstring BasePath = winrt::Windows::ApplicationModel::Package::Current().InstalledPath();
    std::wstring LogoPath = std::wstring(BasePath.data(), BasePath.size()) + L"\\Assets\\StoreLogo.scale-125.png";
    return SHStrDupW(LogoPath.c_str(), ppszIcon);
}

HRESULT OpenTerminalHere::GetFlags(EXPCMDFLAGS* pFlags)
{
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

HRESULT OpenTerminalHere::GetCanonicalName(GUID* pguidCommandName)
{
    *pguidCommandName = __uuidof(this);
    return S_OK;
}

HRESULT OpenTerminalHere::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    *ppEnum = nullptr;
    return E_NOTIMPL;
}