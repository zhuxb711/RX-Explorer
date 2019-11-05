#pragma once
#include "InstructionSet.h"
#include <windows.h>
#include <malloc.h>    
#include <stdio.h>
#include <d3d11.h>
#include <dxgi.h>

using namespace Platform;

#define  GBYTES  1073741824  
#define  MBYTES  1048576  
#define  KBYTES  1024  
#define  DKBYTES 1024.0 
typedef BOOL(WINAPI* LPFN_GLPI)(
	PSYSTEM_LOGICAL_PROCESSOR_INFORMATION,
	PDWORD);

namespace SystemInformationProvider
{
    public ref class SystemInformation sealed
    {
	public:
		property static String^ CPUName
		{
			String^ get()
			{
				std::string CPUName = InstructionSet::Brand();
				std::wstring w_str = std::wstring(CPUName.begin(), CPUName.end());
				const wchar_t* w_chars = w_str.c_str();
				return ref new Platform::String(w_chars);
			}
		}

		property static bool SSE
		{
			bool get()
			{
				return InstructionSet::SSE;
			}
		}

		property static bool SSE2
		{
			bool get()
			{
				return InstructionSet::SSE2;
			}
		}

		property static bool SSE3
		{
			bool get()
			{
				return InstructionSet::SSE3;
			}
		}

		property static bool SSSE3
		{
			bool get()
			{
				return InstructionSet::SSSE3;
			}
		}

		property static bool SSE41
		{
			bool get()
			{
				return InstructionSet::SSE41;
			}
		}

		property static bool SSE42
		{
			bool get()
			{
				return InstructionSet::SSE42;
			}
		}

		property static bool AES
		{
			bool get()
			{
				return InstructionSet::AES;
			}
		}

		property static bool AVX
		{
			bool get()
			{
				return InstructionSet::AVX;
			}
		}

		property static bool AVX2
		{
			bool get()
			{
				return InstructionSet::AVX2;
			}
		}

		property static bool AVX512
		{
			bool get()
			{
				return InstructionSet::AVX512CD || InstructionSet::AVX512ER || InstructionSet::AVX512F || InstructionSet::AVX512PF;
			}
		}

		property static bool MMX
		{
			bool get()
			{
				return InstructionSet::MMX;
			}
		}

		property static bool FMA
		{
			bool get()
			{
				return InstructionSet::FMA;
			}
		}

		property static bool SEP
		{
			bool get()
			{
				return InstructionSet::SEP;
			}
		}

		property static bool SHA
		{
			bool get()
			{
				return InstructionSet::SHA;
			}
		}

		property static String^ MemoryInfo
		{
			String^ get()
			{
				std::string memory_info;
				MEMORYSTATUSEX statusex;
				statusex.dwLength = sizeof(statusex);
				if (GlobalMemoryStatusEx(&statusex))
				{
					unsigned long long total = 0, remain_total = 0, avl = 0, remain_avl = 0;
					double decimal_total = 0, decimal_avl = 0;
					remain_total = statusex.ullTotalPhys % GBYTES;
					total = statusex.ullTotalPhys / GBYTES;
					avl = statusex.ullAvailPhys / GBYTES;
					remain_avl = statusex.ullAvailPhys % GBYTES;
					if (remain_total > 0)
					{
						decimal_total = (remain_total / MBYTES) / DKBYTES;
					}
					if (remain_avl > 0)
					{
						decimal_avl = (remain_avl / MBYTES) / DKBYTES;
					}

					decimal_total += (double)total;
					decimal_avl += (double)avl;
					char  buffer[256];
					sprintf_s(buffer, 256, "%.2f GB||%.2f GB", decimal_total, decimal_avl);
					memory_info.append(buffer);

					std::wstring w_str = std::wstring(memory_info.begin(), memory_info.end());
					const wchar_t* w_chars = w_str.c_str();
					return ref new Platform::String(w_chars);
				}
				else 
				{
					return ref new String();
				}
			}
		}

		property static String^ CPUCoreInfo
		{
			String^ get()
			{
				LPFN_GLPI glpi;
				BOOL done = FALSE;
				PSYSTEM_LOGICAL_PROCESSOR_INFORMATION buffer = NULL;
				PSYSTEM_LOGICAL_PROCESSOR_INFORMATION ptr = NULL;
				DWORD returnLength = 0;
				DWORD logicalProcessorCount = 0;
				DWORD processorCoreCount = 0;
				DWORD byteOffset = 0;
				PCACHE_DESCRIPTOR Cache;
				DWORD L1SIZE, L2SIZE, L3SIZE;

				MEMORY_BASIC_INFORMATION info = {};
				if (VirtualQuery(VirtualQuery, &info, sizeof(info)))
				{
					auto kernelAddr = (HMODULE)info.AllocationBase;
					glpi = (LPFN_GLPI)GetProcAddress(kernelAddr, "GetLogicalProcessorInformation");
				}
				else 
				{
					glpi = nullptr;
				}

				if (NULL == glpi)
				{
					return ref new String();
				}

				while (!done)
				{
					DWORD rc = glpi(buffer, &returnLength);

					if (FALSE == rc)
					{
						if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
						{
							if (buffer)
							{
								free(buffer);
							}

							buffer = (PSYSTEM_LOGICAL_PROCESSOR_INFORMATION)malloc(returnLength);

							if (NULL == buffer)
							{
								return ref new String();
							}
						}
						else
						{
							return ref new String();
						}
					}
					else
					{
						done = TRUE;
					}
				}

				ptr = buffer;

				while (byteOffset + sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION) <= returnLength)
				{
					switch (ptr->Relationship)
					{
					case RelationProcessorCore:
					{
						processorCoreCount++;

						// A hyperthreaded core supplies more than one logical processor.
						DWORD LSHIFT = sizeof(ULONG_PTR) * 8 - 1;
						DWORD bitSetCount = 0;
						ULONG_PTR bitTest = (ULONG_PTR)1 << LSHIFT;
						DWORD i;

						for (i = 0; i <= LSHIFT; ++i)
						{
							bitSetCount += ((ptr->ProcessorMask & bitTest) ? 1 : 0);
							bitTest /= 2;
						}

						logicalProcessorCount += bitSetCount;
						break;
					}
					case RelationCache:
					{
						Cache = &ptr->Cache;
						if (Cache->Level == 1)
						{
							L1SIZE = Cache->Size;
						}
						else if (Cache->Level == 2)
						{
							L2SIZE = Cache->Size;
						}
						else if (Cache->Level == 3)
						{
							L3SIZE = Cache->Size;
						}
						break;
					}
					}
					byteOffset += sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					ptr++;
				}
				free(buffer);
				processorCoreCount;
				logicalProcessorCount;
				return processorCoreCount.ToString() + "||" + logicalProcessorCount.ToString() + "||" + L1SIZE.ToString() + "||" + L2SIZE.ToString() + "||" + L3SIZE.ToString();
			}
		}

		property static Array<String^>^GraphicAdapterInfo
		{
			Array<String^>^ get()
			{
				D3D_FEATURE_LEVEL featureLevels[] = { D3D_FEATURE_LEVEL_9_1 };
				ID3D11Device* pDevice = nullptr;
				HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, featureLevels, ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &pDevice, nullptr, nullptr);
				if (FAILED(hr))
				{
					return nullptr;
				}

				IDXGIDevice* pDxgiDevice = nullptr;
				hr = pDevice->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&pDxgiDevice));
				if (FAILED(hr))
				{
					return nullptr;
				}

				IDXGIAdapter* pDxgiAdapter = nullptr;
				hr = pDxgiDevice->GetAdapter(&pDxgiAdapter);
				if (FAILED(hr))
				{
					return nullptr;
				}

				IDXGIFactory* pIDxgiFactory = nullptr;
				hr = pDxgiAdapter->GetParent(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&pIDxgiFactory));
				if (FAILED(hr))
				{
					return nullptr;
				}

				UINT count = 0;
				std::vector<IDXGIAdapter*> vAdapters;
				IDXGIAdapter* pAdapter;
				while (pIDxgiFactory->EnumAdapters(count, &pAdapter) != DXGI_ERROR_NOT_FOUND)
				{
					vAdapters.push_back(pAdapter);
					count++;
				}

				if (count > 0)
				{
					auto ResultArray = ref new Array<String^>(count);
					auto i = 0;
					for (auto iterator = vAdapters.begin(); iterator != vAdapters.end(); ++iterator)
					{
						DXGI_ADAPTER_DESC desc;
						(*iterator)->GetDesc(&desc);
						ResultArray[i] = ref new String(desc.Description) + "||" + desc.DedicatedVideoMemory.ToString();
						i++;
					}
					return ResultArray;
				}
				else
				{
					return nullptr;
				}
			}
		}
	private:
		SystemInformation();
    };
}
