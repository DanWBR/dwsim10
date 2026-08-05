using DWSIM.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWSIM.SharedClassesCSharp.FilePicker
{
    public class FilePickerService : IFilePickerService
    {
        private static FilePickerService _instance;
        public static IFilePickerService GetInstance()
        {
            if (_instance == null)
                _instance = new FilePickerService();

            return _instance;
        }


        private Func<IFilePicker> _filePickerFactory = () => throw new InvalidOperationException(
            "No file picker was registered. The host calls SetFilePickerFactory at startup.");
        public void SetFilePickerFactory(Func<IFilePicker> filePickerFactory)
        {
            _filePickerFactory = filePickerFactory;
        }

        private FilePickerService()
        {

        }

        public IFilePicker GetFilePicker()
        {
            return _filePickerFactory();
        }
    }
}
