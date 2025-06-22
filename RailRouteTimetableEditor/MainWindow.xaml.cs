using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RailRouteTimetableEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TimetableView.ItemsSource = _timetable;
            StationListView.ItemsSource = _stations;
            TimeTableBuilder.ItemsSource = _buildingTimetable;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Collision Detection Thread
            Task.Run(() =>
            {
                while (true)
                {
                    var collides = false;
                    try
                    {
                        if (!_timetable.Any())
                        {
                            Thread.Sleep(20000);
                            continue;
                        }

                        var timetable = new List<Train>(_timetable);

                        foreach (var train in timetable)
                        {
                            foreach (var compare in timetable.Where(x => x.HeadCode != train.HeadCode && !x.CollidesWith.Contains(train.HeadCode)))
                            {
                                foreach (var entry in train.TimeTable)
                                {
                                    foreach (var compareEntry in compare.TimeTable)
                                    {
                                        if (entry.Station == compareEntry.Station && entry.Platform == compareEntry.Platform && entry.Platform != 0)
                                        {
                                            if (entry.Arrival == compareEntry.Arrival 
                                                || entry.Departure == compareEntry.Departure)
                                            {
                                                
                                                train.HasCollision = true;
                                                train.CollidesWith += $" {compare.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {entry.Arrival} - {entry.Departure} // {compareEntry.Arrival} - {compareEntry.Departure}";

                                                compare.HasCollision = true;
                                                compare.CollidesWith += $" {train.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {compareEntry.Arrival} - {compareEntry.Departure} // {entry.Arrival} - {entry.Departure}";
                                                collides = true;
                                                
                                            }

                                            if (entry.Arrival < compareEntry.Arrival) 
                                            {
                                                if (entry.Departure > compareEntry.Arrival - TimeSpan.FromMinutes(1))
                                                {                                                    
                                                    train.HasCollision = true;
                                                    train.CollidesWith += $" {compare.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {entry.Arrival} - {entry.Departure} // {compareEntry.Arrival} - {compareEntry.Departure}";

                                                    compare.HasCollision = true;
                                                    compare.CollidesWith += $" {train.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {compareEntry.Arrival} - {compareEntry.Departure} // {entry.Arrival} - {entry.Departure}";
                                                    collides = true;
                                                }                                                
                                            }
                                            else
                                            {
                                                if (entry.Arrival < compareEntry.Departure + TimeSpan.FromMinutes(1))
                                                {                                                    
                                                    train.HasCollision = true;
                                                    train.CollidesWith += $" {compare.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {entry.Arrival} - {entry.Departure} // {compareEntry.Arrival} - {compareEntry.Departure}";

                                                    compare.HasCollision = true;
                                                    compare.CollidesWith += $" {train.HeadCode} - at {entry.Station.Name} platform {entry.Platform}, {compareEntry.Arrival} - {compareEntry.Departure} // {entry.Arrival} - {entry.Departure}";
                                                    collides = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Errors.Text += "Collision detection failure";
                            MessageBox.Show("Error in Collision Detection Process", exc.Message);
                        });
                    }
                    finally
                    {
                        if (collides)
                        {
                            RefreshTimetableList();
                        }
                        
                        Thread.Sleep(2000);
                    }

                }
            });
        }

        ObservableCollection<Station> _stations = new ObservableCollection<Station>();        
        
        ObservableCollection<Train> _timetable = new ObservableCollection<Train>();

        
        Train _currentTrain = new Train();

        int lastCommentedLine = 0;

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = "txt";
            var result = openFileDialog.ShowDialog();
            Classification.ItemsSource = Enum.GetValues(typeof(TrainType));
            
            if (result == true)
            {
                _stations.Clear();
                _timetable.Clear();
                Errors.Text = "";
                Task.Run(() =>
                {
                    using var streamReader = new StreamReader(openFileDialog.FileName);                    

                    var count = 0;
                    while (!streamReader.EndOfStream)
                    {
                        var line = streamReader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            if (count == 0)
                            {
                                if (line != "+++stations")
                                {
                                    MessageBox.Show("This is not a valid trains.txt", "Invalid file", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            // Mark the last commented line for saving later
                            if (line.StartsWith("#"))
                            {
                                lastCommentedLine = count;
                            }

                            // Lines that start # a are station definitions
                            if (Regex.IsMatch(line, "^\\# [a-zA-Z] "))
                            {
                                Station station = ParseStation(line);
                                Application.Current.Dispatcher.Invoke(() => _stations.Add(station));

                            }
                            else if (Regex.IsMatch(line, "^[a-zA-Z0-9]*\\|[a-zA-Z0-9]* (COMMUTER|IC|FREIGHT) [0-9]* [LPC]* X[0-1] :"))
                            {
                                // Timetable entry
                                var trainDescription = line.Split(" : ")[0];


                                var headcode = trainDescription.Split("|")[0];

                                var asArray = trainDescription.Split(" ");

                                var typeDesc = asArray[1];
                                var speedDesc = asArray[2];
                                var composition = asArray[3];
                                var penaltyDesc = asArray[4];

                                TrainType? trainType = null;

                                switch (typeDesc)
                                {
                                    case "COMMUTER":
                                        trainType = TrainType.COMMUTER;
                                        break;
                                    case "IC":
                                        trainType = TrainType.INTERCITY;
                                        break;
                                    case "FREIGHT":
                                        trainType = TrainType.FREIGHT;
                                        break;

                                }

                                if (trainType != null)
                                {
                                    if (composition.All(x => (x == 'L' | x == 'P' | x == 'C')))
                                    {
                                        //Parse the timetable
                                        var timetableEntries = line.Split(" : ")[1];

                                        var entries = timetableEntries.Split(" ");
                                        bool ttError = false;
                                        var ttEntries = new List<TimeTableEntry>();

                                        foreach (var entry in entries)
                                        {
                                            if (string.IsNullOrWhiteSpace(entry))
                                            {
                                                continue;
                                            }

                                            var code = entry.Trim().Split("#");

                                            if (code.Length != 4)
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    Errors.Text += $"Invalid timetable entry {entry} in {headcode}" + Environment.NewLine;
                                                });
                                                
                                                ttError = true;
                                                break;
                                            }

                                            var stationCode = code[0];
                                            var station = _stations.FirstOrDefault(x => x.Id == stationCode);

                                            if (station is null)
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    Errors.Text += $"Invalid timetable entry {entry} in {headcode} - invalid station code {stationCode}" + Environment.NewLine;
                                                });
                                                
                                                ttError = true;
                                                break;
                                            }


                                            if (!int.TryParse(code[1], out var platformCode))
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    Errors.Text += $"Invalid timetable entry {entry} in {headcode} - invalid platform {code[1]} for station {station.Name}" + Environment.NewLine;
                                                });
                                                
                                                ttError = true;
                                                break;
                                            }


                                            if (!station.Platforms.Contains(platformCode))
                                            {
                                                Errors.Text += $"Invalid timetable entry {entry} in {headcode} - invalid platform {platformCode} for station {station.Name}" + Environment.NewLine;
                                                ttError = true;
                                                break;
                                            }

                                            if (!TimeSpan.TryParse(code[2], out var time))
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    Errors.Text += $"Invalid timetable entry {entry} in {headcode} - invalid time {code[2]} for station {station.Name}" + Environment.NewLine;
                                                });
                                                
                                                ttError = true;
                                                break;
                                            }

                                            if (!int.TryParse(code[3], out var timeSpent))
                                            {
                                                Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    Errors.Text += $"Invalid timetable entry {entry} in {headcode} - invalid time spent {code[3]} for station {station.Name}" + Environment.NewLine;
                                                });
                                                
                                                ttError = true;
                                                break;
                                            }



                                            var timeTableEntry = new TimeTableEntry
                                            {
                                                Station = station,
                                                Platform = platformCode,
                                                Arrival = time,
                                                Departure = time.Add(TimeSpan.FromMinutes(timeSpent))
                                            };

                                            ttEntries.Add(timeTableEntry);
                                        }
                                        if (ttError)
                                        {
                                            continue;
                                        }

                                        var train = new Train()
                                        {
                                            HeadCode = headcode,
                                            Classification = trainType.Value,
                                            Composition = composition,
                                            MaxSpeed = int.Parse(speedDesc),
                                            Penalty = penaltyDesc.Contains("0"),
                                            TimeTable = ttEntries
                                        };

                                        if (_timetable.Any(x => x.HeadCode == headcode))
                                        {
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                Errors.Text += "Train with headcode " + headcode + " already exists";
                                            });
                                            
                                            continue;
                                        }
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            _timetable.Add(train);
                                        });
                                        
                                    }
                                    else
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            Errors.Text += "Train with headcode " + headcode + " - invalid composition - " + composition + Environment.NewLine;
                                        });
                                        
                                    }

                                }
                                else
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Errors.Text += "Train with headcode " + headcode + " - invalid type - " + typeDesc + Environment.NewLine;
                                    });
                                    
                                }
                            }
                            else
                            {
                                if (line != "+++timetable" && !line.StartsWith("#") & line != "+++stations")
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Errors.Text += line + Environment.NewLine;
                                    });                                    
                                }
                            }
                        }

                        count++;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileName.Text = openFileDialog.FileName;
                    });
                    
                });
                
            }
        }

        private static Station ParseStation(string? line)
        {
            var split = line.Split('=');
            var id = split[0].Trim().Last();
            var secondSection = split[1].Trim().Split('|');
            var name = secondSection[0].Trim();
            var platforms = secondSection[2].Split(',').Select(x => int.Parse(x));

            var station = new Station()
            {
                Id = id.ToString(),
                Name = name,
                Platforms = platforms.Prepend(0).ToList()
            };
            return station;
        }

        private void NewTrain_Click(object sender, RoutedEventArgs e)
        {
            _currentTrain = new Train();
            PlatformListView.Visibility = Visibility.Collapsed;
            PlatformListView.ItemsSource = null;
            MaxSpeed.Text = string.Empty;
            Headcode.Text = string.Empty;
            Composition.Text = string.Empty;
            _isEditing = false;
            Headcode.IsEnabled = true;
            _buildingTimetable.Clear();
        }

        Station? _selectedStation = null;

        private void StationListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                PlatformListView.Visibility = Visibility.Collapsed;
                PlatformListView.ItemsSource = null;
                _selectedStation = null;
            }
            else
            {
                PlatformListView.Visibility = Visibility.Visible;
                PlatformListView.ItemsSource = ((Station)e.AddedItems[0]).Platforms;
                _selectedStation = ((Station)e.AddedItems[0]);
            }
        }

        ObservableCollection<TimeTableEntry> _buildingTimetable = new ObservableCollection<TimeTableEntry>();

        private void PlatformListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedStation is null)
            {
                return;
            }

            if (e.AddedItems.Count == 0)
            {                
                return;
            }

            var entry = new TimeTableEntry
            {
                Station = _selectedStation,
                Platform = (int)e.AddedItems[0]
            };

            _buildingTimetable.Add(entry);

            var listView = sender as ListView;

            listView.UnselectAll();

        }

        private void Composition_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!e.Text.All(x => x == 'L' || x == 'C' || x == 'P'))
            {
                e.Handled = true;
            }            
        }

        private void MaxSpeed_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!int.TryParse(e.Text, out var _))
            {
                e.Handled = true;
            }
            
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {            
            _currentTrain.Penalty = Penalty.IsChecked.Value;

            if (int.TryParse(MaxSpeed.Text, out var maxSpeed))
            {
                _currentTrain.MaxSpeed = maxSpeed;
            }
            else
            {
                MessageBox.Show("Invalid speed");
                return;
            }
            
            if (string.IsNullOrEmpty(Composition.Text))
            {
                MessageBox.Show("Invalid train composition");
                return;
            }
            _currentTrain.Composition = Composition.Text;


            if (string.IsNullOrEmpty(Headcode.Text))
            {
                MessageBox.Show("Invalid Headcode");
            }
            _currentTrain.HeadCode = Headcode.Text;

            try
            {
                _currentTrain.Classification = (TrainType)Classification.SelectedItem;
            }
            catch
            {
                MessageBox.Show("Invalid Train Type");
                return;
            }            

            if (!_isEditing && _timetable.Any(x => x.HeadCode == _currentTrain.HeadCode))
            {
                MessageBox.Show(_currentTrain.HeadCode + " already exists");
                return;
            }

            if (_buildingTimetable.Count < 2)
            {
                MessageBox.Show("Must have at least two timetable entries");
                return;
            }
            _currentTrain.TimeTable = _buildingTimetable.ToList();

            // Force the collision thread to recheck other trains
            if (_currentTrain.HasCollision)
            {
                var trainsToRecheck = _timetable.Where(x => _currentTrain.CollidesWith.Contains(x.HeadCode));
                foreach (var train in trainsToRecheck)
                {
                    train.HasCollision = false;
                    train.CollidesWith = string.Empty;
                }
            }

            _currentTrain.HasCollision = false;
            _currentTrain.CollidesWith = string.Empty;            

            if (_isEditing)
            {
                var toReplace = _timetable.Where(x => x.HeadCode == _currentTrain.HeadCode).FirstOrDefault();
                var index = _timetable.IndexOf(toReplace);
                if (index != -1)
                {
                    _timetable[index] = _currentTrain;
                }                
            }
            else
            {
                _timetable.Add(_currentTrain);
            }
            RefreshTimetableList();
        }

        private void RefreshTimetableList()
        {
            var timetable = _timetable.AsEnumerable();

            if (filterByCollision)
            {
                timetable = _timetable.Where(x => x.HasCollision);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TimetableView.ItemsSource = timetable;
                });
            }

            if (sortByHeadcode == 0)
            {
                if (sortByArrival == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable;
                    });
                    
                }
                else if (sortByArrival == 1)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderBy(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });
                    
                }
                else if (sortByArrival == 2)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderByDescending(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });
                    
                }
            }
            else if (sortByHeadcode == 1)
            {
                if (sortByArrival == 0)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderBy(x => x.HeadCode);
                    });
                    
                }
                else if (sortByArrival == 1)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderBy(x => x.HeadCode).ThenBy(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });
                    
                }
                else if (sortByArrival == 2)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderBy(x => x.HeadCode).ThenByDescending(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });
                    
                }
            }
            else if (sortByHeadcode == 2)
            {
                if (sortByArrival == 0)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderByDescending(x => x.HeadCode);
                    });
                    
                }
                else if (sortByArrival == 1)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderByDescending(x => x.HeadCode).ThenBy(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });
                    
                }
                else if (sortByArrival == 2)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        TimetableView.ItemsSource = timetable.OrderByDescending(x => x.HeadCode).ThenByDescending(x => x.TimeTable.OrderBy(y => y.Arrival).First().Arrival);
                    });                    
                }
            }                        
        }

        int sortByHeadcode = 0;

        private void SortByHeadCode_Click(object sender, RoutedEventArgs e)
        {
            if (sortByHeadcode == 0)
            {
                sortByHeadcode = 1;                
            } 
            else if (sortByHeadcode == 1)
            {
                sortByHeadcode = 2;                
            } 
            else if (sortByHeadcode == 2)
            {
                sortByHeadcode = 0;                
            }
            RefreshTimetableList();

        }

        int sortByArrival = 0;

        private void SortByEntryTime_Click(object sender, RoutedEventArgs e)
        {
            if (sortByArrival == 0)
            {
                sortByArrival = 1;                
            }
            else if (sortByArrival == 1)
            {
                sortByArrival = 2;                
            }
            else if (sortByArrival == 2)
            {
                sortByArrival = 0;                
            }
            RefreshTimetableList();
        }

        bool filterByCollision = false;

        private void SortByCollision_Click(object sender, RoutedEventArgs e)
        {
            filterByCollision = !filterByCollision;
            RefreshTimetableList();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            _buildingTimetable.Remove((TimeTableEntry)(((Button)sender).Tag));
        }

        bool _isEditing = false;

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            _isEditing = true;
            _currentTrain = (Train)((Button)sender).Tag;
            Penalty.IsChecked = _currentTrain.Penalty;
            Headcode.Text = _currentTrain.HeadCode;
            Headcode.IsEnabled = false;
            MaxSpeed.Text = _currentTrain.MaxSpeed.ToString();
            Composition.Text = _currentTrain.Composition;
            Classification.SelectedItem = _currentTrain.Classification;

            _buildingTimetable.Clear();
            _currentTrain.TimeTable.ForEach(_buildingTimetable.Add);
            
        }

        private void Delete_Click_1(object sender, RoutedEventArgs e)
        {
            var train = (Train)((Button)sender).Tag;

            var result = MessageBox.Show($"Are you sure you want to delete {train.HeadCode}?", "Delete Train", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Force the collision thread to recheck other trains
                if (train.HasCollision)
                {
                    var trainsToRecheck = _timetable.Where(x => train.CollidesWith.Contains(x.HeadCode));
                    foreach (var checkTrain in trainsToRecheck)
                    {
                        checkTrain.HasCollision = false;
                        checkTrain.CollidesWith = string.Empty;
                    }
                }

                _timetable.Remove(train);
                RefreshTimetableList();
            }
        }

        private void UpdateProgress(int from, int to, int count, int of)
        {
            var progress = (to - from) * (count / of);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress.Value = progress;
            });
        }

        private void SaveFile(string fileName)
        {
            var originalFileName = FileName.Text;
            Progress.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                var lines = new List<string>();
                // Read the commented lines from the original file - these won't change.
                using var streamReader = new StreamReader(originalFileName);
                int count = 0;
                while (count <= lastCommentedLine && !streamReader.EndOfStream)
                {
                    lines.Add(streamReader.ReadLine());
                    count++;
                    UpdateProgress(0, 10, count, lastCommentedLine);
                }
                streamReader.Close();
                streamReader.Dispose();

                using var streamWriter = new StreamWriter(fileName, false);
                int index = 0;
                foreach (var line in lines) {
                    streamWriter.WriteLine(line);
                    index++;
                    UpdateProgress(10, 20, index, lines.Count);
                }

                int trainIndex = 0;
                foreach (var train in _timetable)
                {
                    var penalty = train.Penalty ? "X0" : "X1";
                    var classification = train.Classification switch
                    {
                        TrainType.INTERCITY => "IC",
                        TrainType.FREIGHT => "FREIGHT",
                        TrainType.COMMUTER => "COMMUTER"                        
                    };

                    var line = train.HeadCode + "|" + train.HeadCode + " " + classification + " " + train.MaxSpeed + " " + train.Composition + " " + penalty + " :";

                    foreach (var entry in train.TimeTable)
                    {
                        var waitTime = entry.Departure - entry.Arrival;

                        line += " " + entry.Station.Id + "#" + entry.Platform + "#" + entry.Arrival + "#" + (int)waitTime.TotalMinutes;
                    }

                    streamWriter.WriteLine(line);
                    trainIndex++;
                    UpdateProgress(20, 100, trainIndex, _timetable.Count);
                }

                MessageBox.Show($"Saved successfully to {fileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Dispatcher.Invoke(() => Progress.Visibility = Visibility.Hidden);
            });
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileName.Text))
            {
                var result = MessageBox.Show("Are you sure you want to override this trains.txt - it cannot be undone", "Override File?", MessageBoxButton.YesNoCancel, MessageBoxImage.Asterisk);
                if (result == MessageBoxResult.Yes)
                {
                    SaveFile(FileName.Text);
                }
                else if (result == MessageBoxResult.No)
                {
                    SaveAs_Click(sender, e);
                }
            }
            else
            {
                MessageBox.Show("Must open an existing trains.txt for this application to work");
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(FileName.Text))
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = "trains.txt|*.txt";
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.Title = "Save trains.txt";

                var result = saveFileDialog.ShowDialog();

                if (result!.Value)
                {
                    SaveFile(saveFileDialog.FileName);
                }
            }
            else
            {
                MessageBox.Show("Must open an existing trains.txt for this application to work");
            }
            
        }

        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox.SelectionLength == 0)
            {
                textBox.SelectAll();
            }
        }

        private void TextBox_LostMouseCapture(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox.SelectionLength == 0)
            {
                textBox.SelectAll();
            }
            textBox.LostMouseCapture -= TextBox_LostMouseCapture;
        }

        private void TextBox_GotMouseCapture(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox.LostMouseCapture += TextBox_LostMouseCapture;
        }

        private void Search(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                RefreshTimetableList();
                return;
            }

            var filtered = _timetable.Where(x => x.HeadCode.ToLower().Contains(text.ToLower()));
            Application.Current.Dispatcher.Invoke(() =>
            {
                TimetableView.ItemsSource = filtered;
            });
            
        }

        Timer waitingTimer;

        private void HeadcodeFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var text = textBox.Text;
            waitingTimer = new Timer(p =>
            {
                Search(text);
            });
            waitingTimer.Change(1000, Timeout.Infinite);
        }

        private void ViewCollisions_Click(object sender, RoutedEventArgs e)
        {
            var train = (sender as Button)?.DataContext as Train;
            
            if (train != null)
            {
                MessageBox.Show(train.CollidesWith, "Collision Detail");
            }            
        }

        private void PlatformEdit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var timeTableEntry = (sender as TextBox)?.DataContext as TimeTableEntry;

            if (int.TryParse(e.Text, out var platform)) {
                if (timeTableEntry != null)
                {
                    var viablePlatforms = timeTableEntry?.Station.Platforms;

                    e.Handled = !viablePlatforms?.Contains(platform) ?? false;
                }
            }
            else
            {
                e.Handled = true;
            }            
        }
    }
}