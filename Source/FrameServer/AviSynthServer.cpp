
#include "AviSynthServer.h"


///////// IUnknown

HRESULT __stdcall AviSynthServer::QueryInterface(const IID& iid, void** ppv)
{
    if (!ppv)
        return E_POINTER;

    if (iid == IID_IUnknown || iid == IID_IFrameServer)
    {
        *ppv = this;
        AddRef();
        return S_OK;
    }

    *ppv = NULL;
    return E_NOINTERFACE;
}


ULONG __stdcall AviSynthServer::AddRef()
{
    return ++m_References;
}


ULONG __stdcall AviSynthServer::Release() {
    int refs = --m_References;

    if (!refs)
        delete this;

    return refs;
}


////////// IFrameServer

HRESULT __stdcall AviSynthServer::OpenFile(WCHAR* file)
{
    try
    {
        memset(&m_Clip,  0, sizeof(PClip));
        memset(&m_Frame, 0, sizeof(PVideoFrame));

        WCHAR* dllPath = _wgetenv(L"AviSynthDLL");
        HMODULE dll;

        if (FileExists(dllPath))
            dll = LoadLibrary(dllPath);
        else
            dll = LoadLibrary(L"AviSynth.dll");

        if (!dll)
        {
            std::string msg = GetWinErrorMessage(GetLastError());
            throw std::runtime_error("Failed to load AviSynth+: \r\n\r\n" + msg);
        }

        typedef IScriptEnvironment* (__stdcall* cse_t)(int);
        cse_t create_env = reinterpret_cast<cse_t>(GetProcAddress(dll, "CreateScriptEnvironment"));

        if (!create_env)
            throw std::exception("Failed to get CreateScriptEnvironment");

        m_ScriptEnvironment = create_env(6 /*AVS_INTERFACE_VERSION*/);

        if (!m_ScriptEnvironment)
            throw std::exception("A newer AviSynth version is required");

        AVS_linkage = m_Linkage = m_ScriptEnvironment->GetAVSLinkage();

        std::string ansiFile = ConvertWideToANSI(file);
        AVSValue arg(ansiFile.c_str());
        m_AVSValue = m_ScriptEnvironment->Invoke("Import", AVSValue(&arg, 1));

        if (!m_AVSValue.IsClip())
            throw std::exception("AviSynth+ script does not return a video clip");

        m_Clip = m_AVSValue.AsClip();

        VideoInfo vi = m_Clip->GetVideoInfo();

        m_Info.Width = vi.width;
        m_Info.Height = vi.height;
        m_Info.FrameCount = vi.num_frames;
        m_Info.FrameRateNum = vi.fps_numerator;
        m_Info.FrameRateDen = vi.fps_denominator;
        m_Info.ColorSpace = vi.pixel_type;
        m_Info.BitsPerPixel = vi.BitsPerPixel();
        
        return S_OK;
    }
    catch (AvisynthError& e)
    {
        m_Error = ConvertAnsiToWide(e.msg);
    }
    catch (std::exception& e)
    {
        m_Error = ConvertAnsiToWide(e.what());
    }
    catch (...)
    {
        m_Error = L"Exception: AviSynthServer::OpenFile";
    }

    Free();
    return E_FAIL;
}


HRESULT __stdcall AviSynthServer::GetFrame(int position, void** data, int& pitch)
{
    try
    {
        if (m_Info.FrameCount == 0)
            return E_FAIL;

        AVS_linkage = m_Linkage;
        m_Frame = m_Clip->GetFrame(position, m_ScriptEnvironment);
        *data = (void*)m_Frame->GetReadPtr();

        if (!(*data))
            throw std::exception("AviSynth+ pixel data pointer is null");
        
        pitch = m_Frame->GetPitch();
        return S_OK;
    }
    catch (AvisynthError& e)
    {
        m_Error = ConvertAnsiToWide(e.msg);
    }
    catch (std::exception& e)
    {
        m_Error = ConvertAnsiToWide(e.what());
    }
    catch (...)
    {
        m_Error = L"Exception: AviSynthServer::GetFrame";
    }

    return NULL;
}


ServerInfo* __stdcall AviSynthServer::GetInfo()
{
    return &m_Info;
}


WCHAR* __stdcall AviSynthServer::GetError()
{
    return (WCHAR*)m_Error.c_str();
}


/////////// local

AviSynthServer::~AviSynthServer()
{
    Free();
}


void AviSynthServer::Free()
{
    AVS_linkage = m_Linkage;

    m_Frame     = NULL;
    m_Clip      = NULL;
    m_AVSValue  = NULL;

    if (m_ScriptEnvironment)
    {
        m_ScriptEnvironment->DeleteScriptEnvironment();
        m_ScriptEnvironment = NULL;
    }

    AVS_linkage = NULL;
    m_Linkage = NULL;
}


///////// extern

extern "C" __declspec(dllexport) AviSynthServer* __stdcall
CreateAviSynthServer()
{
    AviSynthServer* server = new AviSynthServer();
    server->AddRef();
    return server;
}
