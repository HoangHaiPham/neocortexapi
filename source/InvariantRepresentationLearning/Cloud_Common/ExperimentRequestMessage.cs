using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cloud_Common
{
    public class ExerimentRequestMessage
    {
        //public string ExperimentId { get; set; }
        //public string Name { get; set; }
        //public string Description { get; set; }
        //public string InputFile { get; set; }

        public int IMAGE_WIDTH { get; set; }
        public int IMAGE_HEIGHT { get; set; }
        public int FRAME_WIDTH { get; set; }
        public int FRAME_HEIGHT { get; set; }
        public int PIXEL_SHIFTED { get; set; }
        public int MAX_CYCLE { get; set; }
        public int NUM_IMAGES_PER_LABEL { get; set; }
        public int PER_TESTSET { get; set; }

    }

    //public class InputFileParameters
    //{
    //    public string Mode { get; set; }
    //    public int iteration { get; set; }
    //    public int minVal { get; set; }
    //    public int maxVal { get; set; }
    //}
}
