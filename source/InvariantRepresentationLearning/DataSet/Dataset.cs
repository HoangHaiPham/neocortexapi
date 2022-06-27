﻿namespace dataSet
{
    public class DataSet
    {
        public List<string> classes;

        public List<Picture> images;

        public Random random;
        public DataSet(string pathToTrainingFolder)
        {
            random = new Random(42);

            images = new List<Picture>();
            // Getting the classes
            ClassesInit(pathToTrainingFolder);

            // Reading the images from path
            foreach (var classFolder in Directory.GetDirectories(pathToTrainingFolder))
            {
                string label = Path.GetFileName(classFolder);
                foreach (var imagePath in Directory.GetFiles(classFolder))
                {
                    images.Add(new Picture(imagePath, label));
                }
            }
        }

        public int Count { get { return images.Count; } }

        /// <summary>
        /// adding classes to the Training Data's Class(Label) List
        /// </summary>
        /// <param name="pathToTrainingFolder"></param>
        private void ClassesInit(string pathToTrainingFolder)
        {
            classes = new List<string>();
            foreach (var a in Directory.GetDirectories(pathToTrainingFolder))
            {
                classes.Add(Path.GetFileNameWithoutExtension(a));
            }
        }

        /// <summary>
        /// Pick a random element in the set, with a specified seed
        /// </summary>
        /// <param name="seed"></param>
        /// <returns></returns>
        public Picture PickRandom(int seed = 42)
        {
            int index = random.Next(this.Count);
            return images[index];
        }
    }
}