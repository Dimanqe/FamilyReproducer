#region

using System.ComponentModel;
using System.Windows.Input;
using Autodesk.Revit.UI;
using FamilyReproducer.Handlers;
using FamilyReproducer.Models;

#endregion

namespace FamilyReproducer.ViewModels
{
    /// <summary>
    ///     ViewModel для главного окна приложения,
    ///     управляющий состоянием и командами взаимодействия с UI.
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly ExternalEvent _externalEvent;
        private readonly FamilyOpener _familyOpener;
        private readonly ExternalEvent _familyOpenEvent;
        private readonly FamilyReproducerExternalEventHandler _handler;
        private FamilyData _familyData;
        private string _filePath;

        /// <summary>
        ///     Конструктор ViewModel.
        /// </summary>
        /// <param name="externalEvent">Внешнее событие для взаимодействия с Revit API.</param>
        /// <param name="handler">Обработчик внешнего события для воспроизведения семейства.</param>
        public MainWindowViewModel(ExternalEvent externalEvent, FamilyReproducerExternalEventHandler handler)
        {
            _externalEvent = externalEvent;
            _handler = handler;

            ReproduceFamilyCommand = new RelayCommand(ExecuteReproduceFamily, CanExecuteReproduceFamily);

            _familyOpener = new FamilyOpener();
            _familyOpenEvent = ExternalEvent.Create(_familyOpener);
        }

        /// <summary>
        ///     Команда воспроизведения семейства, вызываемая из UI.
        /// </summary>
        public ICommand ReproduceFamilyCommand { get; }

        /// <summary>
        ///     Путь к файлу семейства, выбранному пользователем.
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        /// <summary>
        ///     Данные семейства, извлечённые из файла.
        /// </summary>
        public FamilyData FamilyData
        {
            get => _familyData;
            set
            {
                if (_familyData != value)
                {
                    _familyData = value;
                    OnPropertyChanged(nameof(FamilyData));
                }
            }
        }

        /// <summary>
        ///     Событие, уведомляющее об изменении свойства (для привязки данных).
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Обработчик выполнения команды воспроизведения семейства.
        ///     Запускает обработчик внешнего события с текущими данными.
        /// </summary>
        /// <param name="parameter">Параметр команды (ожидается FamilyData).</param>
        private void ExecuteReproduceFamily(object parameter)
        {
            if (parameter is FamilyData data)
            {
                _handler.Data = data;
                _externalEvent.Raise();
            }
        }

        /// <summary>
        ///     Метод проверки возможности выполнения команды воспроизведения.
        ///     Команда доступна только при наличии параметра.
        /// </summary>
        /// <param name="parameter">Параметр команды.</param>
        /// <returns>True, если команда может быть выполнена.</returns>
        private bool CanExecuteReproduceFamily(object parameter)
        {
            return parameter != null;
        }

        /// <summary>
        ///     Вызывает событие PropertyChanged для обновления UI при изменении свойства.
        /// </summary>
        /// <param name="propertyName">Имя изменённого свойства.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}