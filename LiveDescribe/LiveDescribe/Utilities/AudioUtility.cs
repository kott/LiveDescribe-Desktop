﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LiveDescribe.Utilities
{
    class AudioUtility
    {
        private struct Header
        {
            public byte[] chunkID; 
            public uint chunkSize; 
            public byte[] fmt; 
            public byte[] subChunk1ID; 
            public uint subChunk1Size;
            public ushort audioFormat;
            public ushort numChannels;
            public uint sampleRate;
            public uint byteRate; 
            public ushort blockAlign; 
            public ushort bitsPerSample;   
            public byte[] subChunk2ID;
            public uint subChunk2Size;
        }

        private string _videoFile;
        private string _audioFile;
        private Header _header = new Header();
        private List<double> _audioData;
 
        public AudioUtility(string videoFile)
        {
            this._videoFile = videoFile;
            this._audioFile = Path.GetFileNameWithoutExtension(_videoFile) + ".wav";
        }


        /// <summary>
        /// Strips the audio from the given video file and outputs a wave file using FFMPEG.
        /// </summary>
        /// <remarks>
        /// This function uses the executable FFMPEG file to handle the audio stripping
        /// </remarks>
        public void stripAudio()
        {
            //gets the path of the ffmpeg.exe file within the LiveDescribe solution
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string ffmpegPath = Path.Combine(appDirectory, "Utilities/ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show("Cannot find ffmpeg.exe" + " " + ffmpegPath);
                return;
            }

            //the parameters that are sent to the ffmpeg command
            string strParam = " -i \"" + this._videoFile + "\" -ac 2 -ab 160k -ar 44100 -f wav -vn -y \"" + this._audioFile + "\"";

            var ffmpeg = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = ffmpegPath,
                    Arguments = strParam,
                    CreateNoWindow = true
                }
            };

            ffmpeg.Start();

            string text = null;
            double totalTime = 0;
            double currentTime = 0;
            //stream reader used to parse the output of the ffmpeg process
            StreamReader input = ffmpeg.StandardError;


            /* Parsing the output of ffmpeg to obtain the total time and the current time 
             to  calculate a percentage whose value is used to update the progress bar*/
            try
            {
                while (!input.EndOfStream)
                {
                    text = input.ReadLine();
                    string word = "";
                    for (int i = 0; i < text.Length; i++)
                    {
                        word += text[i];
                        if (text[i] == ' ')
                        {
                            if (word.Equals("Duration: "))
                            {
                                int currentIndex = i + 1;
                                string time = "";

                                for (int j = currentIndex; j < currentIndex + 11; j++)
                                {
                                    time += text[j];
                                }

                                totalTime = getTime(time);
                            }
                            word = "";
                        }

                        if (text[i] == '=')
                        {
                            if (word.Equals("time="))
                            {
                                int currentIndex = i + 1;
                                string time = "";

                                for (int j = currentIndex; j < currentIndex + 11; j++)
                                {
                                    time += text[j];
                                }

                                currentTime = getTime(time);
                            }
                        }
                    }

                    //updates the progress bar given that the total time is not zero
                    if (totalTime != 0)
                    {
                        int percentComplete = Convert.ToInt32(((double)currentTime / (double)totalTime) * 100);
                        if (percentComplete <= 100)
                        {
                            Console.WriteLine("Percentage: " + percentComplete);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


            ffmpeg.WaitForExit();

            var fileInfo = new FileInfo(this._audioFile);
            Console.WriteLine("Audio file length: " + fileInfo.Length);
        }

        /// <summary>
        /// Convert from ffmpeg time to seconds 
        /// </summary>
        /// <param name="time">the time in ffmpeg format HH:MM:SS</param>
        /// <returns></returns>
        private double getTime(string time)
        {
            double hours;
            double minutes;
            double seconds;

            hours = Convert.ToDouble(time.Substring(0, 2)) * 60 * 60;
            minutes = Convert.ToDouble(time.Substring(3, 2)) * 60;
            seconds = Convert.ToDouble(time.Substring(6, 2));

            return hours + minutes + seconds;
        }


        /// <summary>
        /// Read the wav data from the stripped audio and return the sample data
        /// </summary>
        /// <returns>data</returns>
        public List<double> readWavData()
        {
            double maxSampleValue = Math.Pow(2,16);
            byte[] buffer;
            double val;
            List<double> data = new List<double>();

            using (FileStream fs = new FileStream(this._audioFile, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                try
                {
                    //RIFF chunk
                    this._header.chunkID = br.ReadBytes(4);
                    this._header.chunkSize = br.ReadUInt32();
                    this._header.fmt = br.ReadBytes(4);

                    //fmt sub chunk
                    this._header.subChunk1ID = br.ReadBytes(4);
                    this._header.subChunk1Size = br.ReadUInt32();
                    this._header.audioFormat = br.ReadUInt16();
                    this._header.numChannels = br.ReadUInt16();
                    this._header.sampleRate = br.ReadUInt32();
                    this._header.byteRate = br.ReadUInt32();
                    this._header.blockAlign = br.ReadUInt16();
                    this._header.bitsPerSample = br.ReadUInt16();

                    br.ReadBytes(2); //skip two bytes, this shouldn't need to happen

                    //data sub chunk
                    this._header.subChunk2ID = br.ReadBytes(4);
                    this._header.subChunk2Size = br.ReadUInt32();

                    //add the sample values to the data list
                    for (int i = 0; i < this._header.subChunk2Size / this._header.blockAlign; i++)
                    {
                        buffer = br.ReadBytes(2);
                       // val = (double)((buffer[0] & 0xff) | (buffer[1] << 8));// / maxSampleValue; //normalized sample value
                        val = (double)((BitConverter.ToInt16(buffer, 0) + (maxSampleValue/2)) / maxSampleValue);
                        if (this._header.numChannels == 2)
                        {
                            //skip next sample 
                            br.ReadBytes(2);
                        }

                        data.Add(val);
                        buffer = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    if (br != null)
                    {
                        br.Close();
                    }
                    if (fs != null)
                    {
                        fs.Close();
                    }
                }
            }

            //Display the Contents of _header
            Console.WriteLine("ChunkID: " + System.Text.Encoding.Default.GetString(this._header.chunkID));
            Console.WriteLine("Chunksize: " + this._header.chunkSize);
            Console.WriteLine("fmt: " + System.Text.Encoding.Default.GetString(this._header.fmt));
            Console.WriteLine("subChunk1ID: " + System.Text.Encoding.Default.GetString(this._header.subChunk1ID));
            Console.WriteLine("subChunk1Size: " + this._header.subChunk1Size);
            Console.WriteLine("audioFormat: " + this._header.audioFormat);
            Console.WriteLine("numChannels: " + this._header.numChannels);
            Console.WriteLine("sampleRate: " + this._header.sampleRate);
            Console.WriteLine("byteRate: " + this._header.byteRate);
            Console.WriteLine("blockAlign: " + this._header.blockAlign);
            Console.WriteLine("bitsPerSample: " + this._header.bitsPerSample);
            Console.WriteLine("subChunk2Id: " + System.Text.Encoding.Default.GetString(this._header.subChunk2ID));
            Console.WriteLine("subChunk2Size: " + this._header.subChunk2Size);
            
            foreach (double sample in data)
            {
                if (sample < 0.3)
                {
                    Console.WriteLine("Sample Value: " + sample);
                }
                
            }
           
            return data;
        }
    }
}
