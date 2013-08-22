using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace AudioIN
{
	public class Microphone
	{
		#region Fields

		/// <summary>
		/// The OpenAL audio capture device
		/// This will be NULL if shit is fucked up
		/// </summary>
		AudioCapture audio_capture;

		/// <summary>
		/// The buffer to hold audio data.  
		/// </summary>
		private byte[] buffer;

		/// <summary>
		/// Flag used by the threading thing to check if it should keep spinning and checking mic data
		/// </summary>
		private bool continuePolling = false;

		/// <summary>
		/// A thread for doing all the microhpone audio processing
		/// </summary>
		Thread workerThread = null;

		/// <summary>
		/// The number of bytes in one sample.
		/// </summary>
		private int SampleToByte;

		/// <summary>
		/// An openal source???
		/// </summary>
		private int src;

		/// <summary>
		/// Sample rate used to capture data per second
		/// </summary>
		private const int audioSamplingRate = 8000;

		/// <summary>
		/// Number of samples to collect (8000 samples per second / number of Samples = 64 ms of audio capture)
		/// </summary>
		private const int numberOfSamples = 512;

		/// <summary>
		/// Gain of microphone
		/// </summary>
		private const float microphoneGain = 4.0f;

		#region MicHandle Shit

		/// <summary>
		/// Used to average recent volume.
		/// </summary>
		private List <float> dbValues;

		/// <summary>
		/// The sound volume of the current frame. (stored in dannobels).
		/// </summary>
		private float m_fCurrentVolume = 0.0f;

		/// <summary>
		/// Used to find the max volume from the last x number of samples.
		/// </summary>
		private float m_fMaxVolume = 0.0f;

		#endregion //MicHandle Shit

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initialize the microphone
		/// </summary>
		/// <param name="samplingRate">Sample rate used during capture</param>
		/// <param name="gain">Gain of the microphone</param>
		/// <param name="deviceCaptureName">Name of the Device used for capturing audio</param>
		/// <param name="format">One of the openAL formats</param>
		/// <param name="bufferSize">Size of the buffer</param>
		public Microphone()
		{
			InitializeMicrophone(audioSamplingRate, microphoneGain, AudioCapture.DefaultDevice, ALFormat.Mono16, numberOfSamples);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Gets a value indicating whether this instance is microphone valid.
		/// </summary>
		/// <value><c>true</c> if this instance is microphone valid; otherwise, <c>false</c>.</value>
		private bool IsMicrophoneValid
		{
			get
			{
				return (null != audio_capture);
			}
		}

		#endregion Properties

		#region Methods

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		public void StartRecording()
		{
			if (IsMicrophoneValid)
			{
				//Start capturing data
				audio_capture.Start();
				continuePolling = true;

				//Spin up the thread to process all the mic data
				if (workerThread == null || workerThread.IsAlive == false)
				{
					workerThread = new Thread(PollMicrophoneForData);
					workerThread.Start();
				}
			}
		}

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		public void StopRecording()
		{
			if (IsMicrophoneValid)
			{
				continuePolling = false;
				audio_capture.Stop();
				ClearBuffers(0);
			}
		}

		/// <summary>
		/// Clears the Microphone buffers
		/// </summary>
		/// <param name="input">Which buffer</param>
		private void ClearBuffers(int input)
		{
			int[] freedbuffers;
			if (input == 0)
			{
				int BuffersProcessed;
				AL.GetSource(src, ALGetSourcei.BuffersProcessed, out BuffersProcessed);
				if (BuffersProcessed == 0)
					return;
				freedbuffers = AL.SourceUnqueueBuffers(src, BuffersProcessed);
			}
			else
			{
				freedbuffers = AL.SourceUnqueueBuffers(src, input);
			}
			AL.DeleteBuffers(freedbuffers);
		}

		private bool InitializeMicrophone(int samplingRate, float gain, string deviceCaptureName, ALFormat format, int bufferSize)
		{
			AL.Listener(ALListenerf.Gain, gain);

			src = AL.GenSource();

			SampleToByte = NumberOfBytesPerSample(format);

			buffer = new byte[bufferSize * SampleToByte];

			try
			{
				audio_capture = new AudioCapture(deviceCaptureName, samplingRate, format, bufferSize);
			}
			catch (AudioDeviceException ade)
			{
				audio_capture = null;
			}

			if (audio_capture == null)
				return false;

			return true;
		}

		private static int NumberOfBytesPerSample(ALFormat format)
		{
			switch (format)
			{
				case ALFormat.Mono16:
				return 2;
				case ALFormat.Mono8:
				return 1;
				case ALFormat.MonoALawExt:
				return 1;
				case ALFormat.MonoDoubleExt:
				return 8;
				case ALFormat.MonoFloat32Ext:
				return 4;
				case ALFormat.MonoIma4Ext:
				return 4;
				case ALFormat.MonoMuLawExt:
				return 1;
				case ALFormat.Mp3Ext:
				return 2; //Guessed might not be correct
				case ALFormat.Multi51Chn16Ext:
				return 6 * 2;
				case ALFormat.Multi51Chn32Ext:
				return 6 * 4;
				case ALFormat.Multi51Chn8Ext:
				return 6 * 1;
				case ALFormat.Multi61Chn16Ext:
				return 7 * 2;
				case ALFormat.Multi61Chn32Ext:
				return 7 * 4;
				case ALFormat.Multi61Chn8Ext:
				return 7 * 1;
				case ALFormat.Multi71Chn16Ext:
				return 7 * 2;
				case ALFormat.Multi71Chn32Ext:
				return 7 * 4;
				case ALFormat.Multi71Chn8Ext:
				return 7 * 1;
				case ALFormat.MultiQuad16Ext:
				return 4 * 2;
				case ALFormat.MultiQuad32Ext:
				return 4 * 4;
				case ALFormat.MultiQuad8Ext:
				return 4 * 1;
				case ALFormat.MultiRear16Ext:
				return 1 * 2;
				case ALFormat.MultiRear32Ext:
				return 1 * 4;
				case ALFormat.MultiRear8Ext:
				return 1 * 1;
				case ALFormat.Stereo16:
				return 2 * 2;
				case ALFormat.Stereo8:
				return 2 * 1;
				case ALFormat.StereoALawExt:
				return 2 * 1;
				case ALFormat.StereoDoubleExt:
				return  2 * 8;
				case ALFormat.StereoFloat32Ext:
				return  2 * 4;
				case ALFormat.StereoIma4Ext:
				return  1; //Guessed
				case ALFormat.StereoMuLawExt:
				return  2 * 1;
				case ALFormat.VorbisExt:
				return  2; //Guessed
				default:
				return 2;
			}
		}

		/// <summary>
		/// Used to poll the Microphone for data
		/// </summary>
		private void PollMicrophoneForData()
		{
			while (continuePolling)
			{
				Thread.Sleep(1); //Allow GUI some time

				if (audio_capture.AvailableSamples * SampleToByte >= buffer.Length)
				{
					UpdateSamples();
				}
			}
		}

		private void UpdateSamples()
		{
			buffer = new byte[buffer.Length];
			audio_capture.ReadSamples(buffer, buffer.Length / SampleToByte); //Need to divide as the readsamples expects the value to be in 2 bytes.

			//Queue raw data, let receiving application determine if it needs to compress
			this.microphoneData.Enqueue(buffer);
			ClearBuffers(0);
		}

		#endregion Methods
	}
}