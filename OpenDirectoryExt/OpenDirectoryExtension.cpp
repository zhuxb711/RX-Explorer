#include "pch.h"
#include "OpenDirectoryExtension.h"
#include <wil/filesystem.h>
#include <winrt\Windows.Storage.h>
#include <winrt\Windows.Foundation.Collections.h>
#include "resource.h"

static constexpr std::wstring_view VerbName{ L"RxExplorerOpenHere" };
static constexpr std::wstring_view DefaultDisplayName{ L"Open in RX-Explorer" };

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
    DWORD count;
    psiItemArray->GetCount(&count);

    winrt::com_ptr<IShellItem> psi;
    RETURN_IF_FAILED(psiItemArray->GetItemAt(0, psi.put()));

    wil::unique_cotaskmem_string pszName;
    RETURN_IF_FAILED(psi->GetDisplayName(SIGDN_FILESYSPATH, &pszName));

    wil::unique_process_information _piClient;
    STARTUPINFOEX siEx{ 0 };
    siEx.StartupInfo.cb = sizeof(STARTUPINFOEX);

    std::wstring cmdline = L"RX-Explorer.exe \"" + std::wstring(pszName.get()) + L"\"";

    RETURN_IF_WIN32_BOOL_FALSE(CreateProcessW(
        nullptr, //lpApplicationName
        cmdline.data(), //lpCommandLine
        nullptr, // lpProcessAttributes
        nullptr, // lpThreadAttributes
        false, // bInheritHandles
        EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, // dwCreationFlags
        nullptr, // lpEnvironment
        nullptr, //lpCurrentDictionary
        &siEx.StartupInfo, // lpStartupInfo
        &_piClient // lpProcessInformation
    ));

    return S_OK;
}

HRESULT OpenTerminalHere::GetToolTip(IShellItemArray* /*psiItemArray*/,
    LPWSTR* ppszInfoTip)
{
    winrt::hstring DisplayName = winrt::unbox_value_or<winrt::hstring>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"GlobalizationStringForContextMenu"), DefaultDisplayName);
    return SHStrDupW(DisplayName.data(), ppszInfoTip);
}

HRESULT OpenTerminalHere::GetTitle(IShellItemArray* /*psiItemArray*/,
    LPWSTR* ppszName)
{
    winrt::hstring DisplayName = winrt::unbox_value_or<winrt::hstring>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"GlobalizationStringForContextMenu"), DefaultDisplayName);
    return SHStrDupW(DisplayName.data(), ppszName);
}

HRESULT OpenTerminalHere::GetState(IShellItemArray* /*psiItemArray*/,
    BOOL /*fOkToBeSlow*/,
    EXPCMDSTATE* pCmdState)
{
    // compute the visibility of the verb here, respect "fOkToBeSlow" if this is
    // slow (does IO for example) when called with fOkToBeSlow == FALSE return
    // E_PENDING and this object will be called back on a background thread with
    // fOkToBeSlow == TRUE

    // We however don't need to bother with any of that, so we'll just return
    // ECS_ENABLED.

    const bool enabled = winrt::unbox_value_or<bool>(winrt::Windows::Storage::ApplicationData::Current().LocalSettings().Values().Lookup(L"IntegrateWithWindowsExplorerContextMenu"), true);
    *pCmdState = enabled ? ECS_ENABLED : ECS_HIDDEN;
    return S_OK;
}

STDMETHODIMP_(HRESULT __stdcall) OpenTerminalHere::GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon)
{
    winrt::hstring BasePath = winrt::Windows::ApplicationModel::Package::Current().InstalledPath();
    std::wstring LogoPath = std::wstring(BasePath.data(), BasePath.size()) + L"\\Assets\\StoreLogo.scale-125.png";
    return SHStrDupW(LogoPath.data(), ppszIcon);
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