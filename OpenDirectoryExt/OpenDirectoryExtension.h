#pragma once
//#include <ShObjIdl.h>

using namespace Microsoft::WRL;

struct __declspec(uuid("B4CEA422-3911-4198-16CB-63345D563096"))
    OpenTerminalHere : public RuntimeClass<RuntimeClassFlags<ClassicCom | InhibitFtmBase>, IExplorerCommand>
{
#pragma region IExplorerCommand
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
#pragma endregion
};

CoCreatableClass(OpenTerminalHere);