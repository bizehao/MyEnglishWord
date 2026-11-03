using System;
using System.Collections.Generic;
using System.Text;
using Windows.Media.Capture;

namespace MyEnglishWord.Helpers;

public class AudioCapturePermissions
{
    private static readonly int NoCaptureDevicesHResult = -1072845856;

    /// <summary>
    /// Checks whether the microphone is available and the app has permission to use it.
    /// Perform this check every time the app gets focus, because the user can change
    /// the setting while the app is suspended or not in focus.
    /// </summary>
    /// <returns>True if the microphone is available.</returns>
    public static async Task<bool> RequestMicrophonePermission()
    {
        try
        {
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                MediaCategory = MediaCategory.Speech
            };
            var capture = new MediaCapture();
            await capture.InitializeAsync(settings);
        }
        catch (TypeLoadException)
        {
            // Media player components are not available.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Permission to use the audio capture device is denied.
            return false;
        }
        catch (Exception exception)
        {
            if (exception.HResult == NoCaptureDevicesHResult)
            {
                // No audio capture devices are present on this system.
                return false;
            }
            throw;
        }
        return true;
    }
}
