#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
//#define _VSDESIGNER_DONT_LOAD_AS_DLL
#define WINAPI_FAMILY WINAPI_FAMILY_DESKTOP_APP
//#include <LibraryIncludes.h>


#include <unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.ApplicationModel.h>

#include <Shobjidl.h>
#include <shlwapi.h>
#pragma comment(lib,"shlwapi.lib")

#include <wrl.h>
#include <wrl\module.h>
