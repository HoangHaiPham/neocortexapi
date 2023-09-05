using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cloud_Common
{
    public class ExerimentRequestMessage
    {
        public int IMAGE_WIDTH { get; set; }
        public int IMAGE_HEIGHT { get; set; }
        public int FRAME_WIDTH { get; set; }
        public int FRAME_HEIGHT { get; set; }
        public int PIXEL_SHIFTED { get; set; }
        public int MAX_CYCLE { get; set; }
        public int NUM_IMAGES_PER_LABEL { get; set; }
        public int PER_TESTSET { get; set; }
    }
}
