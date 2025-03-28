#pragma once

using namespace Microsoft::WRL;
using namespace winrt::Windows::ApplicationModel::Resources::Core;

struct __declspec(uuid("B4CEA422-3911-4198-16CB-63345D563096")) OpenDirectoryExtension : public RuntimeClass<RuntimeClassFlags<ClassicCom | InhibitFtmBase>, IExplorerCommand>
{
public:
    STDMETHODIMP Invoke(IShellItemArray* psiItemArray,
        IBindCtx* pBindContext);
    STDMETHODIMP GetToolTip(IShellItemArray* psiItemArray,
        LPWSTR* ppszInfoTip);
    STDMETHODIMP GetTitle(IShellItemArray* psiItemArray,
        LPWSTR* ppszName);
    STDMETHODIMP GetState(IShellItemArray* psiItemArray,
        BOOL fOkToBeSlow,
        EXPCMDSTATE* pCmdState);
    STDMETHODIMP GetIcon(IShellItemArray* psiItemArray,
        LPWSTR* ppszIcon);
    STDMETHODIMP GetFlags(EXPCMDFLAGS* pFlags);
    STDMETHODIMP GetCanonicalName(GUID* pguidCommandName);
    STDMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum);

private:
    HRESULT GetLocalizedString(LPCWSTR resName, LPWSTR* ppszOutput);
};

CoCreatableClass(OpenDirectoryExtension);