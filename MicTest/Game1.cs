using System;
using System.Threading;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Input;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System.Diagnostics;

namespace MicTest
{
	/// <summary>
	/// This is the main type for your game
	/// </summary>
	public class Game1 : Game
	{
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;

		#region Audio Out

		//Create the audio out component
		AudioOUT.PlayAudio playAudio;

		#endregion

		#region Audio In

		//Create the audio in component
		AudioIN.Microphone microphone;

		//Thread used to poll microphone for data
		Thread MicrophoneCapturedDataThread = null;



		//Used to export incoming voice data to wav format
		MemoryStream memoryStreamAudioSaveBuffer = new MemoryStream();

		#endregion

		object lockThis = new object();
		public delegate void InvokeDelegate();

		public Game1()
		{
			graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			graphics.IsFullScreen = true;
		}

		/// <summary>
		/// Allows the game to perform any initialization it needs to before starting to run.
		/// This is where it can query for any required services and load any non-graphic
		/// related content.  Calling base.Initialize will enumerate through any components
		/// and initialize them as well.
		/// </summary>
		protected override void Initialize()
		{
			// TODO: Add your initialization logic here
			base.Initialize();
				
		}

		/// <summary>
		/// LoadContent will be called once per game and is the place to load
		/// all of your content.
		/// </summary>
		protected override void LoadContent()
		{
			// Create a new SpriteBatch, which can be used to draw textures.
			spriteBatch = new SpriteBatch(GraphicsDevice);

			//TODO: use this.Content to load your game content here 
		}

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update(GameTime gameTime)
		{
			// For Mobile devices, this logic will close the Game when the Back button is pressed
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
			{
				Exit();
			}
			// TODO: Add your update logic here			
			base.Update(gameTime);
		}

		/// <summary>
		/// This is called when the game should draw itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Draw(GameTime gameTime)
		{
			graphics.GraphicsDevice.Clear(Color.CornflowerBlue);
		
			//TODO: Add your drawing code here
            
			base.Draw(gameTime);
		}

		#region Methods

		private void InitializeRadioCommunications()
		{
			//Can be used to find out all of the capture devices on the system.
			//IList<string> audioCaptureDevices = AudioCapture.AvailableDevices;
			//foreach (var item in audioCaptureDevices)
			//{
			//    string availableCaptureDevices = item.ToString();
			//}

			//Initialize the Microphone using the default microphone
			StartMicrophone(audioSamplingRate, microphoneGain, AudioCapture.DefaultDevice, ALFormat.Mono16, numberOfSamples * 2);

			//Start the process to send out microphone data through the socket
			StartThreadSendingMicrophoneSoundToSignalPDU();

			//Initialize the audio player
			playAudio = new AudioOUT.PlayAudio();
		}

		/// <summary>
		/// Used to ensure that all started threads and processes are shut down correctly
		/// </summary>
		private void CompleteTest_FormClosing()
		{
			if (microphone != null)
			{
				microphone.StopRecording();
			}

			if (playAudio != null)
			{
				playAudio.StopPlayBack();
				playAudio = null;
			}
		}

		/// <summary>
		/// Intializes the microphone
		/// </summary>
		/// <param name="samplingRate">Sample rate of recording</param>
		/// <param name="microphoneGain">Software gain of microphone</param>
		/// <param name="deviceCaptureName">System name of device used to capture audio</param>
		/// <param name="format">Format of recorded data</param>
		/// <param name="bufferSize">Size of buffer</param>
		private void StartMicrophone(int samplingRate, float microphoneGain, string deviceCaptureName, ALFormat format, int bufferSize)
		{
			microphone = new AudioIN.Microphone(samplingRate, microphoneGain, deviceCaptureName, format, bufferSize);

			if (microphone.isMicrophoneValid == false)
			{
				Debug.WriteLine("ERROR SETTING UP MICROPHONE");
			}
			else
			{
				//Start accepting input from the microphone
				microphone.StartRecording();
			}
		}

		/// <summary>
		/// Threaded method that will continue to poll socket for data
		/// </summary>
		private void RetreiveDataFromSocket()
		{
			byte pdu_type;
			byte pdu_version;

			//Continue to process data received until false
			while (continueReceivingSocketData == true)
			{
				//Using queued collection to determine if data has arrived
				if (receiveBroadCast.pduQueue.Count > 0)
				{
					//Process any PDUs (note that a collection was used as multiple pdu's can be sent in one packet)
					List<byte[]> pdus = pduReceive.ProcessRawPDU(receiveBroadCast.pduQueue.Dequeue(), endianType);

					int countPDU = 0;
					int pduCount = pdus.Count;

					while (countPDU < pduCount && continueReceivingSocketData == true)
					{
						byte[] objPdu = pdus[countPDU];
						pdu_type = objPdu[PDU_TYPE_POSITION]; //get pdu type

						pdu_version = objPdu[PDU_VERSION_POSITION];//what version (currently not processing anything but DIS 1998)

						//Cast as radio pdu, as receive socket method will throw out all other types of PDUs
						DIS1998net.RadioCommunicationsFamilyPdu pduReceived = pduReceive.ConvertByteArrayToPDU1998(pdu_type, objPdu, endianType) as DIS1998net.RadioCommunicationsFamilyPdu;

						SiteHostEntityRadioFrequency siteHostEntityRadioFrequency = new SiteHostEntityRadioFrequency(pduReceived.EntityId.Site, pduReceived.EntityId.Application, pduReceived.EntityId.Entity, pduReceived.RadioId, 0);

						if (pduReceived.EntityId != entityID || (pduReceived.EntityId == entityID && this.isLoopBackAudioEnabled == true))
						{
							//Transmitter which contains the frequency of the signal pdu
							if (pdu_type == 25)
							{

								//Update transmitter packets received
								UpdatedTransmitterPacketsReceived();

								DIS1998net.TransmitterPdu transmitterPDU_Received = (DIS1998net.TransmitterPdu)pduReceived;

								//Assign the frequency from the Transmitter PDU just received.
								siteHostEntityRadioFrequency.frequency = transmitterPDU_Received.Frequency;

								//Remove the frequency from the collection as it will be added back later if it is transmitting
								siteHostEntityRadioFrequencyCollection.Remove(siteHostEntityRadioFrequencyCollection.Find(i => i.application == siteHostEntityRadioFrequency.application && i.entity == siteHostEntityRadioFrequency.entity && i.radioID == siteHostEntityRadioFrequency.radioID && i.site == siteHostEntityRadioFrequency.site));

								//If transmitter is transmitting then add to collection
								if (transmitterPDU_Received.TransmitStateEnumeration == DIS1998net.TransmitterPdu.TransmitStateEnum.TransmitterOnTransmitting)
								{
									siteHostEntityRadioFrequencyCollection.Add(siteHostEntityRadioFrequency);
								}

								foreach (FrequencySpeakerLocationTransmitterReceiverActiveClass item in FrequencySpeakerLocationTransmitterReceiverActive)
								{
									if (siteHostEntityRadioFrequencyCollection.Exists(i => i.frequency == item.Frequency) == true)
									{
										ChangeRadioRecievingColorIndicator(item.UniqueID,true);
									}
									else
										ChangeRadioRecievingColorIndicator(item.UniqueID, false);
								}
							}


							//is it a signal PDU?
							if (pdu_type == 26)
							{
								//Update signal packets received
								UpdatedSignalPacketsReceived();

								DIS1998net.SignalPdu signalPDU = (DIS1998net.SignalPdu)pduReceived;

								//Does the current signal pdu match one in the transmitter collection
								if (siteHostEntityRadioFrequencyCollection.Exists(i => i.application == siteHostEntityRadioFrequency.application && i.entity == siteHostEntityRadioFrequency.entity && i.radioID == siteHostEntityRadioFrequency.radioID && i.site == siteHostEntityRadioFrequency.site) == true)
								{
									//Retrieve the saved frequency
									SiteHostEntityRadioFrequency storedSitehostEntityRadioFrequency = siteHostEntityRadioFrequencyCollection.Find(i => i.application == siteHostEntityRadioFrequency.application && i.entity == siteHostEntityRadioFrequency.entity && i.radioID == siteHostEntityRadioFrequency.radioID && i.site == siteHostEntityRadioFrequency.site);

									//Transmitter was transmitting at this frequency, need to check to see if it is one that we will playback
									siteHostEntityRadioFrequency.frequency = storedSitehostEntityRadioFrequency.frequency;

									if (FrequencyOnPlayBackList(storedSitehostEntityRadioFrequency.frequency) == true)
									{
										//Need to retrieve the speaker location that matches the frequency
										FrequencySpeakerLocationTransmitterReceiverActiveClass retrievedFreqSpeakerTransmitterReceiver = FrequencySpeakerLocationTransmitterReceiverActive.Find(i => i.Frequency == storedSitehostEntityRadioFrequency.frequency);

										//Decode the data, only implemented uLaw 
										byte[] unEncodedData = uLaw.Decode(((DIS1998net.SignalPdu)pduReceived).Data, 0, ((DIS1998net.SignalPdu)pduReceived).Data.Length);

										//Used to save to a stream
										//ms.Seek(ms.Length, SeekOrigin.Begin);
										//ms.Write(encodedData, 0, encodedData.Length);

										//Play back unencoded data
										playAudio.PlayBackAudio(unEncodedData, ALFormat.Mono16, (int)signalPDU.SampleRate, retrievedFreqSpeakerTransmitterReceiver.SpeakerLocation);
									}
								}
							}
						}

						countPDU++;

						//Give GUI time to do things
						Thread.Sleep(1);
					}
				}
				//Give GUI time to do things
				Thread.Sleep(1);
			}
		}


		#endregion Methods
	}
}

