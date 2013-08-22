using System;

namespace MicTest
{
	using UnityEngine;
	using System.Collections;
	using System.Collections.Generic;

	public class MicHandle : MonoBehaviour
	{
		#region Fields

		public static MicHandle GetInstance;

		private const int FREQUENCY = 48000;    // Wavelength, I think.
		private const int SAMPLECOUNT = 1024;   // Sample Count.

		public int recordedLength = 30;    // How many previous frames of sound are analyzed.

		private float [] samples;           // Samples
		private List <float> dbValues;      // Used to average recent volume.

		/// <summary>
		/// The sound volume of the current frame. (stored in dannobels).
		/// </summary>
		private float m_fCurrentVolume = 0.0f;

		/// <summary>
		/// Used to find the max volume from the last x number of samples.
		/// </summary>
		private float m_fMaxVolume = 0.0f;

		#endregion //Fields

		#region Properties

		public float Volume
		{
			get { return m_fCurrentVolume; }
		}

		public float AverageVolume
		{
			get { return m_fMaxVolume; }
		}

		#endregion //Properties

		public void Start()
		{
			StartMicListener ();
		}

		public void Awake()
		{
			GetInstance = this;

			samples = new float [SAMPLECOUNT];
			dbValues = new List <float> ();
		}

		public void Update()
		{
			// If the audio has stopped playing, this will restart the mic play the clip.
			if (!audio.isPlaying)
			{
				StartMicListener();
			}

			// Gets volume and pitch values
			AnalyzeSound();

			//Run a series of algorithms to decide whether a player is talking.
			DeriveIsTalking();
		}

		/// Starts the Mic, and plays the audio back in (near) real-time.
		private void StartMicListener()
		{
			audio.clip = Microphone.Start ( "Built-in Microphone", true , 999, FREQUENCY);

			// HACK - Forces the function to wait until the microphone has started, before moving onto the play function.
			while (!(Microphone.GetPosition("Built-in Microphone" ) > 0))
			{
			}

			audio.Play();
		}

		/// Credits to aldonaletto for the function, http://goo.gl/VGwKt
		/// Analyzes the sound, to get volume and pitch values.
		private void AnalyzeSound()
		{
			// Get all of our samples from the mic.
			audio.GetOutputData(samples, 0);

			//Get the largest waveform from all the current samples.
			float fMaxDb = 0.0f;
			for (int i = 0; i < SAMPLECOUNT; i++)
			{
				float fAbs = Mathf.Abs(samples[i]);
				if (fAbs > fMaxDb)
				{
					fMaxDb = fAbs;
				}
			}

			//Set the current volume to the loudest sound
			m_fCurrentVolume = fMaxDb;
		}

		// Updates a record, by removing the oldest entry and adding the newest value (val).
		private void UpdateRecords(float val, List<float > record)
		{
			while (record.Count > recordedLength)
			{
				record.RemoveAt(0);
			}
			record.Add(val);
		}

		/// <summary>
		/// Figure out if the player is talking by averaging out the recent values of volume
		/// </summary>
		private void DeriveIsTalking()
		{
			UpdateRecords(Volume, dbValues);

			//Find the largest value in the current list
			m_fMaxVolume = 0.0f;
			for (int i = 0; i < dbValues.Count; i++)
			{
				if (dbValues[i] > m_fMaxVolume)
				{
					m_fMaxVolume = dbValues[i];
				}
			}
		}
	}
}

