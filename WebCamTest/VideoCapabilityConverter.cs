using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using AForge.Video.DirectShow;

namespace WebCamTest
{
	internal class VideoCapabilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			VideoCapabilities capabilities = (VideoCapabilities) value;
			return
				$"{capabilities.FrameSize.Width}x{capabilities.FrameSize.Height}x{capabilities.BitCount}bpp @ {capabilities.MaximumFrameRate}fps";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
