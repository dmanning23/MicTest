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
		/// Boolean value indicating if microphone intialized correctly
		/// </summary>
		public bool isMicrophoneValid = false;

		AudioCapture audio_capture;

		private byte[] buffer = new byte[1024];

		private bool continuePolling = false;

		private Queue<byte[]> microphoneData = new Queue<byte[]>();

		Thread pollMicrophone = null;

		private int SampleToByte = 2;

		private int src;

		//Sample rate used to capture data per second
		private int audioSamplingRate = 8000;

		//Number of samples to collect (8000 samples per second / number of Samples = 64 ms of audio capture)
		private int numberOfSamples = 512;

		//Gain of microphone
		private float microphoneGain = 4.0f;

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
			isMicrophoneValid = InitializeMicrophone(audioSamplingRate, microphoneGain, AudioCapture.DefaultDevice, ALFormat.Mono16, numberOfSamples * 2);
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		/// Queue used to hold the incoming data from the microphone.
		/// </summary>
		public Queue<byte[]> MicrophoneData
		{
			get
			{
				return microphoneData;
			}

			set
			{
				microphoneData = value;
			}
		}

		#endregion Properties

		#region Methods

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		public void StartRecording()
		{
			if (isMicrophoneValid == true)
			{
				audio_capture.Start();
				continuePolling = true;

				if (pollMicrophone == null || pollMicrophone.IsAlive == false)
				{
					pollMicrophone = new Thread(PollMicrophoneForData);
					pollMicrophone.Start();
				}
			}
		}

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		public void StopRecording()
		{
			if (isMicrophoneValid == true)
			{
				continuePolling = false;
				audio_capture.Stop();
				ClearBuffers(0);
				MicrophoneData.Clear();
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

			buffer = new byte[bufferSize];

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

		private int NumberOfBytesPerSample(ALFormat format)
		{
			int bytesPerSample = 2;

			switch (format)
			{
				case ALFormat.Mono16:
				bytesPerSample = 2;
				break;
				case ALFormat.Mono8:
				bytesPerSample = 1;
				break;
				case ALFormat.MonoALawExt:
				bytesPerSample = 1;
				break;
				case ALFormat.MonoDoubleExt:
				bytesPerSample = 8;
				break;
				case ALFormat.MonoFloat32Ext:
				bytesPerSample = 4;
				break;
				case ALFormat.MonoIma4Ext:
				bytesPerSample = 4;
				break;
				case ALFormat.MonoMuLawExt:
				bytesPerSample = 1;
				break;
				case ALFormat.Mp3Ext:
				bytesPerSample = 2; //Guessed might not be correct
				break;
				case ALFormat.Multi51Chn16Ext:
				bytesPerSample = 6 * 2;
				break;
				case ALFormat.Multi51Chn32Ext:
				bytesPerSample = 6 * 4;
				break;
				case ALFormat.Multi51Chn8Ext:
				bytesPerSample = 6 * 1;
				break;
				case ALFormat.Multi61Chn16Ext:
				bytesPerSample = 7 * 2;
				break;
				case ALFormat.Multi61Chn32Ext:
				bytesPerSample = 7 * 4;
				break;
				case ALFormat.Multi61Chn8Ext:
				bytesPerSample = 7 * 1;
				break;
				case ALFormat.Multi71Chn16Ext:
				bytesPerSample = 7 * 2;
				break;
				case ALFormat.Multi71Chn32Ext:
				bytesPerSample = 7 * 4;
				break;
				case ALFormat.Multi71Chn8Ext:
				bytesPerSample = 7 * 1;
				break;
				case ALFormat.MultiQuad16Ext:
				bytesPerSample = 4 * 2;
				break;
				case ALFormat.MultiQuad32Ext:
				bytesPerSample = 4 * 4;
				break;
				case ALFormat.MultiQuad8Ext:
				bytesPerSample = 4 * 1;
				break;
				case ALFormat.MultiRear16Ext:
				bytesPerSample = 1 * 2;
				break;
				case ALFormat.MultiRear32Ext:
				bytesPerSample = 1 * 4;
				break;
				case ALFormat.MultiRear8Ext:
				bytesPerSample = 1 * 1;
				break;
				case ALFormat.Stereo16:
				bytesPerSample = 2 * 2;
				break;
				case ALFormat.Stereo8:
				bytesPerSample = 2 * 1;
				break;
				case ALFormat.StereoALawExt:
				bytesPerSample = 2 * 1;
				break;
				case ALFormat.StereoDoubleExt:
				bytesPerSample = 2 * 8;
				break;
				case ALFormat.StereoFloat32Ext:
				bytesPerSample = 2 * 4;
				break;
				case ALFormat.StereoIma4Ext:
				bytesPerSample = 1; //Guessed
				break;
				case ALFormat.StereoMuLawExt:
				bytesPerSample = 2 * 1;
				break;
				case ALFormat.VorbisExt:
				bytesPerSample = 2; //Guessed
				break;
				default:
				break;
			}

			return bytesPerSample;
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