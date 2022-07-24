using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using Kompas6API5;
using KompasAPI7;
using Kompas6Constants;
using System.Runtime.InteropServices;
using Kompas6Constants3D;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using ExtrusionSketch.Properties;

namespace ExtrusionSketch.ViewModels
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<string> _log = new();

        [ObservableProperty]
        private string? _folderDirectory;

        [ObservableProperty]
        private string? _statusBar;

        [ObservableProperty]
        private bool _isAllDirectories = Settings.Default.IsAllDirectories;

        #region Команды
        [ICommand]
        private void StartExtrusions()
        {
            Log.Clear(); //Очистка журнала
            if (!Directory.Exists(FolderDirectory))
            {
                StatusBar = "Путь к папке не корректен.";
                return;
            }

            if (Type.GetTypeFromProgID("KOMPAS.Application.7", true) is not Type kompasType)
            {
                StatusBar = "Не удалось запустить компас";
                return;
            }
            //Запуск компаса
            if (Activator.CreateInstance(kompasType) is not IApplication application)
            {
                StatusBar = "Не удалось запустить компас";
                return;
            }
            application.Visible = true;
            IDocuments documents = application.Documents;

            #region Выбор поиска по всем папкам, или только в папке верхнего уровня
            SearchOption searchOption;
            if (IsAllDirectories)
            {
                searchOption = SearchOption.AllDirectories;
            }
            else
            {
                searchOption = SearchOption.TopDirectoryOnly;
            }
            #endregion

            foreach (string filepath in Directory.GetFiles(FolderDirectory, "*.frw", searchOption))
            {
                IKompasDocument3D kompasDocument3D = (IKompasDocument3D)documents.Add(DocumentTypeEnum.ksDocumentPart, true);//Создаем документ 3D деталь
                IPart7 part7 = kompasDocument3D.TopPart;
                IPlane3D planeYOZ = (IPlane3D)part7.DefaultObject[ksObj3dTypeEnum.o3d_planeYOZ];
                IModelContainer modelContainer = (IModelContainer)part7;
                ISketchs sketchs = modelContainer.Sketchs;
                ISketch sketch = sketchs.Add();
                sketch.Angle = 90;
                sketch.Plane = planeYOZ; //Эскиз будет размещаться на плоскости "Спереди"
                IKompasDocument kompasDocument = sketch.BeginEdit(); //Начало формирования эскиза
                //Получаем фрагмент для вставки
                IKompasDocument2D kompasDocument2D = (IKompasDocument2D)kompasDocument;
                IViewsAndLayersManager viewsAndLayersManager = kompasDocument2D.ViewsAndLayersManager;
                IViews views = viewsAndLayersManager.Views;
                IView view = views.ActiveView;
                IDrawingContainer drawingContainer = (IDrawingContainer)view;
                InsertionObjects insertionObjects = drawingContainer.InsertionObjects;
                IInsertionsManager insertionsManager = (IInsertionsManager)kompasDocument;

                if (!File.Exists(filepath))
                {
                    Log.Add($"{filepath} - не удалось открыть файл");
                    kompasDocument3D.Close(DocumentCloseOptions.kdDoNotSaveChanges);
                    break;
                }

                InsertionDefinition insertionDefinition = insertionsManager.AddDefinition(ksInsertionTypeEnum.ksTBodyFragment, "", filepath);
                IInsertionObject insertionObject = insertionObjects.Add(insertionDefinition);
                insertionObject.SetPlacement(0, 0, 0, false); //Вставка фрагмента будет в начало координат
                #region Получаем толщину детали
                IInsertionFragment insertionFragment = (IInsertionFragment)insertionObject;
                IVariable7 variable7 = insertionFragment.Variable["t"];

                string thickness = "";
                if (variable7 != null)
                {
                    thickness = variable7.Value.ToString();
                }
                #endregion
                insertionObject.Update(); //Вставляем фрагмент
                sketch.EndEdit(); //Закончили формировать эскиз
                sketch.Update();

                string fileName = $"{filepath[..^3]}m3d"; //Имя файла для сохранения 3D модели

                if (thickness == "")
                {
                    Log.Add($"{filepath} - не указана толщина");
                    kompasDocument3D.SaveAs(fileName);
                    if (kompasDocument3D.Name == "")
                    {
                        Log.Add($"{filepath} - не удалось сохранить");
                    }
                    kompasDocument3D.Close(DocumentCloseOptions.kdDoNotSaveChanges);
                    break;
                }

                #region Выдавливаем эскиз
                IExtrusions extrusions = modelContainer.Extrusions;
                IExtrusion extrusion = extrusions.Add(ksObj3dTypeEnum.o3d_bossExtrusion);
                extrusion.Direction = ksDirectionTypeEnum.dtMiddlePlane; //Выдавливание "симметрично"
                extrusion.Sketch = (Sketch)sketch;
                extrusion.Depth[true] = Convert.ToDouble(thickness); //Толщина выдавливания
                extrusion.Update();
                kompasDocument3D.SaveAs(fileName);
                if (kompasDocument3D.Name == "")
                {
                    Log.Add($"{filepath} - не удалось сохранить");
                }
                kompasDocument3D.Close(DocumentCloseOptions.kdDoNotSaveChanges);
                #endregion
            }
            application.Quit();
            if (Log.Count == 0)
            {
                StatusBar = "Готово";
            }
            else
            {
                StatusBar = "Готово. Есть ошибки, посмотрите журнал.";
            }

            using StreamWriter sw = new("Log.txt", false);
            foreach (string item in Log)
            {
                sw.WriteLine(item);
            }
            sw.Close();
        }

        [ICommand]
        private void SelectDiretory()
        {
            FolderDirectory = Data.Directory.GetFolder();
        }

        [ICommand]
        private void OpenLog()
        {
            if (File.Exists("Log.txt"))
            {
                Process process = new Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.FileName = "Log.txt";
                process.Start();
            }
            else
            {
                StatusBar = "Файл журнала не найден";
            }
        }

        [ICommand]
        private void SaveSettings()
        {
            Settings.Default.IsAllDirectories = IsAllDirectories;
            Settings.Default.Save();
        } 
        #endregion
    }
}
