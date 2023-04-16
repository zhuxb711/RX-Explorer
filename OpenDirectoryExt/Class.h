#pragma once

#include "Class.g.h"

namespace winrt::OpenDirectoryExt::implementation
{
    struct Class : ClassT<Class>
    {
        Class() = default;
    };
}

namespace winrt::OpenDirectoryExt::factory_implementation
{
    struct Class : ClassT<Class, implementation::Class>
    {

    };
}
