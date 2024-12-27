
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace Convertor
{
    public partial class MainPage : ContentPage, INotifyPropertyChanged // реализовать интерфейс  для уведомлений об изменениях данных

    {
        
        private CurrencyRate _fromCurrency; // из валюты 
        private CurrencyRate _toCurrency; // в валюту 
        private string _infoMessage;  // сообщение ошибки 
        private DateTime _selectedDate=DateTime.Today; // выбранная дата 

        private readonly HttpClient _httpClient = new(); // созадние клиента для запроса 

        //private ObservableCollection<string> _currencies; // коллекция валют автом


        // конструктор 
        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;

            
            //Currencies = new ObservableCollection<string>(); //Создает коллекцию валют
            _ = LoadRatesAsync(); //Асинхронно загружает курсы валют
              // Назначает команду для конвертации валют
           
        }

        public event PropertyChangedEventHandler PropertyChanged;

        

        // свойства с уведомлениями 
        private string _convertedAmount;

        public string ConvertedAmount  // результат конвертации 
        {
            get => _convertedAmount;
            set => SetProperty(ref _convertedAmount, value);
        }

        private decimal _sourceDecimalAmount;

        private string _amount;

        public string Amount  // сумма в исходной валюте
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    if (decimal.TryParse(value, out var amount))
                    {
                        _sourceDecimalAmount = amount;
                        ConvertCurrency();
                    }
                    else
                    {
                        _sourceDecimalAmount = 0;
                    }
                }
            }
        }

        public CurrencyRate FromCurrency  // из валюты 
        {
            get => _fromCurrency;
            set
            {
                if (SetProperty(ref _fromCurrency, value))
                    ConvertCurrency();
            }
        }

        public CurrencyRate ToCurrency  // в валюту 
        {
            get => _toCurrency;
            set
            {
                if (SetProperty(ref _toCurrency, value))
                    ConvertCurrency();
            }
        }



        
        public string InfoMessage // сообщение о ошибки 
        {
                get => _infoMessage;
                set => SetProperty(ref _infoMessage, value);
        }


        public DateTime SelectedDate  // выбранная дата 
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    Debug.WriteLine($"Date changed to: {value}");
                    _ = LoadRatesAsync();
                }
            }
        }

        public ObservableCollection<CurrencyRate> Currencies { get; } = new();

        //public ICommand ConvertCommand { get; } // команда для конвертации валют

        //private async void LoadCurrencies() // Загружает список доступных валют для выбранной даты
        //{
        //    Currencies = new ObservableCollection<string>();
        //    Currencies.Clear(); // ноль 

        //    var response = await GetCurrencyRates(SelectedDate);
        //    Debug.WriteLine($"Загрузка курсов для даты: {SelectedDate}");

        //    if (response != null && response.Value != null)
        //    {
        //        foreach (var currency in response.Value)
        //        {
        //            Currencies.Add(currency.Key);
        //        }
        //    }
        //    else
        //    {
        //        InfoMessage = "Не удалось загрузить валюты.";
        //    }
        //}

        private async Task LoadRatesAsync() // Загружает курсы валют на определенную дату
        {
            DateTime date = SelectedDate;
            Debug.WriteLine($"Загрузка курсов для даты: {date}");

            while (true)
            {
                var rates = await GetCurrencyRates(date);
                if (rates != null)
                {
                    //_ratesCache = rates;
                    InfoMessage = $"Курс на {date:dd.MM.yyyy}";

                    UpdateCurrencies(rates);
                    ConvertCurrency();  

                    SavePreferences();
                    break;
                }
                date = date.AddDays(-1);
                if (date < DateTime.Today.AddYears(-1))
                {
                    Debug.WriteLine("Данные за указанную дату недоступны.");
                    InfoMessage = "Курсы недоступны для выбранной даты";
                    break;
                }
            }
        }

        private void SavePreferences() // Сохраняет данные в локальном хранилище 
        {
            Preferences.Set("SelectedDate", SelectedDate);
            
            Preferences.Set("SourceAmount", Amount);
        }

        private async Task<Dictionary<string, CurrencyRate>> GetCurrencyRates(DateTime date)  // Выполняет запрос для получения курсов валют
        {
            string url = date.Date == DateTime.Today
                ? "https://www.cbr-xml-daily.ru/daily_json.js"
                : $"https://www.cbr-xml-daily.ru/archive/{date:yyyy'%2F'MM'%2F'dd}/daily_json.js"; // yyyy/MM/dd

            Debug.WriteLine($"Запрос URL: {url}");

            try
            {
                var response = await _httpClient.GetFromJsonAsync<JsonElement>(url);
                // var response = JsonSerializer.Deserialize<CurrencyResponse>(json);

                
                if (response.TryGetProperty("Valute", out var valute))
                {
                    var rates = valute.Deserialize<Dictionary<string, CurrencyRate>>() ?? new();

                        rates["RUB"] = new CurrencyRate
                        {
                            CharCode = "RUB",
                            Name = "Российский рубль",
                            Value = 1,
                            Nominal = 1
                        };

                    return  rates ;
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Ошибка загрузки данных: {ex.Message}");
            }

            return null;
        }

        private void UpdateCurrencies(Dictionary<string, CurrencyRate> rates)  // обновляют и сохраняют валюту 
        {
            var previousFromCurrency = FromCurrency?.CharCode;
            var previousToCurrency = ToCurrency?.CharCode;

            Currencies.Clear();
            foreach (var rate in rates.Values)
            {
                Currencies.Add(rate);
            }

            FromCurrency = Currencies.FirstOrDefault(x => x.CharCode == previousFromCurrency);
            ToCurrency = Currencies.FirstOrDefault(x => x.CharCode == previousToCurrency);

        }

        private void ConvertCurrency() // конвертация валют
        {
            if (string.IsNullOrEmpty(FromCurrency?.CharCode) || string.IsNullOrEmpty(ToCurrency?.CharCode))
            {

                return;
            }

            decimal result = (_sourceDecimalAmount * FromCurrency.Value / FromCurrency.Nominal) * ToCurrency.Value / ToCurrency.Nominal;

            ConvertedAmount = result.ToString("F2");

            SavePreferences();
           

        }



        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class CurrencyRate  // данные
        {
            public string CharCode { get; set; }
            public string Name { get; set; }
            public decimal Value { get; set; }
            public decimal Nominal { get; set; } // учитывать 


        }

    }


    
}