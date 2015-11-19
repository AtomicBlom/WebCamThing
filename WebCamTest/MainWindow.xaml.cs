using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using DrawingRectangle = System.Drawing.Rectangle;
using PixelFormat = System.Windows.Media.PixelFormat;
using WinFormsPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace WebCamTest
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public static readonly DependencyProperty CurrentDeviceProperty = DependencyProperty.Register(
			"CurrentDevice",
			typeof (FilterInfo),
			typeof (MainWindow),
			new PropertyMetadata(
				default(FilterInfo),
				(o, args) => ((MainWindow) o).OnCurrentDeviceChanged((FilterInfo) args.NewValue)
				)
			);

		public static readonly DependencyProperty SelectedDeviceCapabilitiesProperty = DependencyProperty.Register(
			"SelectedDeviceCapabilities",
			typeof (VideoCapabilities),
			typeof (MainWindow),
			new PropertyMetadata(
				default(VideoCapabilities),
				(o, args) => ((MainWindow) o).OnSelectedDeviceCapabilitiesChanged((VideoCapabilities) args.NewValue)
				)
			);

		public static readonly DependencyProperty AvailableDevicesProperty = DependencyProperty.Register(
			"AvailableDevices", typeof (ICollection<FilterInfo>), typeof (MainWindow),
			new PropertyMetadata(default(ICollection<FilterInfo>)));

		public static readonly DependencyProperty CurrentDeviceCapabilitiesProperty = DependencyProperty.Register(
			"CurrentDeviceCapabilities", typeof (ICollection<VideoCapabilities>), typeof (MainWindow),
			new PropertyMetadata(default(ICollection<VideoCapabilities>)));

		private VideoCaptureDevice _currentDevice;

		public MainWindow()
		{
			InitializeComponent();

			AvailableDevices = new ObservableCollection<FilterInfo>(
				new FilterInfoCollection(FilterCategory.VideoInputDevice).OfType<FilterInfo>()
				);
		}


		public VideoCapabilities SelectedDeviceCapabilities
		{
			get { return (VideoCapabilities) GetValue(SelectedDeviceCapabilitiesProperty); }
			set { SetValue(SelectedDeviceCapabilitiesProperty, value); }
		}

		public ICollection<VideoCapabilities> CurrentDeviceCapabilities
		{
			get { return (ICollection<VideoCapabilities>) GetValue(CurrentDeviceCapabilitiesProperty); }
			set { SetValue(CurrentDeviceCapabilitiesProperty, value); }
		}

		public FilterInfo CurrentDevice
		{
			get { return (FilterInfo) GetValue(CurrentDeviceProperty); }
			set { SetValue(CurrentDeviceProperty, value); }
		}

		public ICollection<FilterInfo> AvailableDevices
		{
			get { return (ICollection<FilterInfo>) GetValue(AvailableDevicesProperty); }
			set { SetValue(AvailableDevicesProperty, value); }
		}

		private void OnCurrentDeviceChanged(FilterInfo filterInfo)
		{
			if (_currentDevice != null)
			{
				_currentDevice.Stop();
				_currentDevice.NewFrame -= DeviceOnNewFrame;
			}
			_currentDevice = new VideoCaptureDevice(filterInfo.MonikerString);

			CurrentDeviceCapabilities = new ObservableCollection<VideoCapabilities>(_currentDevice.VideoCapabilities);
			SelectedDeviceCapabilities = CurrentDeviceCapabilities.FirstOrDefault();
			
			_currentDevice.NewFrame += DeviceOnNewFrame;
			

		}

		private void OnSelectedDeviceCapabilitiesChanged(VideoCapabilities capabilities)
		{
			if (capabilities != null)
			{
				if (_currentDevice.IsRunning)
				{
					_currentDevice.SignalToStop();
				}
				_currentDevice.VideoResolution = capabilities;
				ThreadPool.QueueUserWorkItem(state =>
						{
							_currentDevice.WaitForStop();
							_currentDevice.Start();
						}
					);
			}
		}

		private void DeviceOnNewFrame(object sender, NewFrameEventArgs eventArgs)
		{
			var bmp = eventArgs.Frame;		

			// Lock the bitmap's bits.  
			var rect = new DrawingRectangle(0, 0, bmp.Width, bmp.Height);
			var bmpData =
				bmp.LockBits(rect, ImageLockMode.ReadWrite,
					bmp.PixelFormat);

			// Get the address of the first line.
			var ptr = bmpData.Scan0;

			// Declare an array to hold the bytes of the bitmap.
			var bytes = Math.Abs(bmpData.Stride)*bmp.Height;
			var rgbValues = new byte[bytes];

			// Copy the RGB values into the array.
			Marshal.Copy(ptr, rgbValues, 0, bytes);

			// Set every third value to 255. A 24bpp bitmap will look red.  
			for (var counter = 2; counter < rgbValues.Length; counter += 3)
				rgbValues[counter] = 255;
			
			// Unlock the bits.
			bmp.UnlockBits(bmpData);
			
			if (Dispatcher.CheckAccess())
			{
				DisplayImage(bmp.Width, bmp.Height, bmp.HorizontalResolution, bmp.VerticalResolution, bmp.PixelFormat, rgbValues);
			}
			else
			{
				try
				{
					Dispatcher.Invoke(() =>
					{
						DisplayImage(bmp.Width, bmp.Height, bmp.HorizontalResolution, bmp.VerticalResolution, bmp.PixelFormat, rgbValues);
					}
						);
				}
				catch (TaskCanceledException) {}
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			_currentDevice.NewFrame -= DeviceOnNewFrame;
			_currentDevice.SignalToStop();
		}

		private void DisplayImage(int width, int height, float dpiX, float dpiY, WinFormsPixelFormat pixelFormat,
			byte[] imageData)
		{
			try
			{
				var wpfPixelFormat = getWpfPixelFormatFromWinformsPixelFormat(pixelFormat);

				var bitsPerPixel = wpfPixelFormat.BitsPerPixel;
				var stride = ((width*bitsPerPixel + (bitsPerPixel - 1)) & ~(bitsPerPixel - 1))/8;

				Parallel.For(0, imageData.Length / 3, index =>
					{
						int i = index*3;
						byte r = imageData[i + 0];
						byte g = imageData[i + 1];
						byte b = imageData[i + 2];

						//var yCbCr = new YCbCr(r / 255.0f, g / 255.0f, b / 255.0f);
						//var rgb = yCbCr.ToRGB();

						imageData[i] = r;
						imageData[i + 1] = g;
						imageData[i + 2] = b;//(byte)(255 - b);
					}
				);

				var bitmapSource = BitmapSource.Create(
					width,
					height,
					dpiX,
					dpiY,
					wpfPixelFormat,
					null,
					imageData,
					stride);

				rawRGB.Width = bitmapSource.Width;
				rawRGB.Height = bitmapSource.Height;
				rawRGB.Source = bitmapSource;
			}
			catch (TaskCanceledException) {}
		}

		private PixelFormat getWpfPixelFormatFromWinformsPixelFormat(WinFormsPixelFormat pixelFormat)
		{
			switch (pixelFormat)
			{
				case WinFormsPixelFormat.Format24bppRgb:
					return PixelFormats.Rgb24;
				default:
					throw new InvalidCastException($"No mapping created for winforms pixel format {pixelFormat}");
			}
		}

		private void DisplayDeviceProperties_OnClick(object sender, RoutedEventArgs e)
		{
			if (_currentDevice != null)
			{
				_currentDevice.DisplayPropertyPage(new WindowInteropHelper(this).Handle);
			}
		}
	}
}