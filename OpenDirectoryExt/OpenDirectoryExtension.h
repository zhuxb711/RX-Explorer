#pragma once
//#include <ShObjIdl.h>

using namespace Microsoft::WRL;

struct __declspec(uuid("e82bd2a8-8d63-42fd-b1ae-d364c201d8a7"))
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