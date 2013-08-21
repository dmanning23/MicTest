using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace AudioOUT
{
	public class PlayAudio
	{
		#region Fields

		ALError alError;

		AudioContext audio_context;

		private Vector3 position = new Vector3();

		//The use of two sources was added do to problems found when transmitting voice continuously (ie. PTT activated for long periods of time)
		//There was a delay in the sound after a period of time.  This occurence only seemed to happen on a Windows system as
		//the linux (SUSE) seemed to keep up with the sound.  I have seen this also in other applications i have created using
		//the Win32 waveOutOpen function. Therefore the use of two sources. There is potential for voices to run ontop of each other but due to the
		//serial nature of the sound comming in I do not believe this will occur.
		private int[] sources = new int[2];

		private int sourcesLeft = 0;

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initialize playback of audio
		/// </summary>
		public PlayAudio()
		{
			IList<string> availableDevices = AudioContext.AvailableDevices;

			foreach (string item in availableDevices)
			{
				string availablePlaybackDevices = item;
			}

			audio_context = new AudioContext();
            
			//Initialize the sources
			for (int i = 0; i < sources.Length; i++)
			{
				sources[i] = AL.GenSource();
			}

			position = SetSpeakerPosition(SpeakerLocation.LeftRight);
		}

		#endregion Constructors

		#region Enumerations

		/// <summary>
		/// Location of speaker output
		/// </summary>
		public enum SpeakerLocation
		{
			Left,
			Right,
			LeftRight
		}

		#endregion Enumerations

		#region Methods

		/// <summary>
		/// Playback the audio
		/// </summary>
		/// <param name="unencodedData">Raw byte data</param>
		/// <param name="recordingFormat">OpenAL sound format</param>
		/// <param name="sampleFrequency">Frequency of the samples</param>
		/// <param name="speakerLocation">Speaker location</param>
		public void PlayBackAudio(byte[] unencodedData, ALFormat recordingFormat, int sampleFrequency, SpeakerLocation speakerLocation)
		{
			//Determine if sources needed to be switched
			if (sourcesLeft == 0)
			{
				sourcesLeft = sources.Length;
			}

			//Used to rotate the sources being used.
			sourcesLeft--;

			int buf = AL.GenBuffer();

			AL.BufferData(buf, recordingFormat, unencodedData, unencodedData.Length, sampleFrequency);

			position = SetSpeakerPosition(speakerLocation);
			AL.Source(sources[sourcesLeft], ALSource3f.Position, ref position);

			AL.SourceQueueBuffer(sources[sourcesLeft], buf);
			if (AL.GetSourceState(sources[sourcesLeft]) != ALSourceState.Playing)
			{
				ClearSourcePlayBackBuffers(sources[sourcesLeft]);
				AL.SourcePlay(sources[sourcesLeft]);
			}

			ClearSourcePlayBackBuffers(sources[sourcesLeft]);
		}

		/// <summary>
		/// Playback a file
		/// </summary>
		/// <param name="unencodedData">Raw byte data</param>
		/// <param name="recordingFormat">Format of data</param>
		/// <param name="sampleFrequency">Frequency sampling of data</param>
		public void PlayFile(byte[] unencodedData, ALFormat recordingFormat, int sampleFrequency)
		{
			int[] uiBuffers = new int[4];
			int uiSource;

			uiBuffers = AL.GenBuffers(uiBuffers.Length);

			uiSource = AL.GenSource();

			alError = AL.GetError();

			AL.BufferData(uiBuffers[0], recordingFormat, unencodedData, unencodedData.Length, sampleFrequency);

			alError = AL.GetError();

			AL.SourceQueueBuffers(uiSource, 1, uiBuffers);

			alError = AL.GetError();
			AL.SourcePlay(uiSource);

			alError = AL.GetError();
		}

		/// <summary>
		/// Stop Audio playback
		/// </summary>
		public void StopPlayBack()
		{
			for (int i = 0; i < sources.Length; i++)
			{
				StopPlayBack(sources[i]); 
			}

			audio_context.Dispose();
		}

		/// <summary>
		/// Stop Audio playback
		/// </summary>
		private void StopPlayBack(int src)
		{
			if (audio_context != null)
			{
				AL.SourceStop(src);
				ClearSourcePlayBackBuffers(src);
			}
		}

		/// <summary>
		/// Clear the buffers from the selected source
		/// </summary>
		/// <param name="source">Source buffer to clear</param>
		private void ClearSourcePlayBackBuffers(int source)
		{
			int BuffersProcessed;
			int[] freedbuffers;
			AL.GetSource(source, ALGetSourcei.BuffersProcessed, out BuffersProcessed);
			if (BuffersProcessed == 0)
				return;

			freedbuffers = AL.SourceUnqueueBuffers(source, BuffersProcessed);

			AL.DeleteBuffers(freedbuffers);
		}

		private Vector3 SetSpeakerPosition(SpeakerLocation speakerLocation)
		{
			Vector3 location = new Vector3();

			if (speakerLocation == SpeakerLocation.LeftRight)
			{
				location = new Vector3(1, 0, 1);
			}

			if (speakerLocation == SpeakerLocation.Left)
			{
				location = new Vector3(-1, 0, 0);
			}

			if (speakerLocation == SpeakerLocation.Right)
			{
				location = new Vector3(1, 0, 0);
			}

			return location;
		}

		#endregion Methods
	}
}