using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace RxNETExample
{
    public partial class Form1 : Form
    {
        private IDisposable _subscription1;
        private IDisposable _subscription2;

        public Form1()
        {
            InitializeComponent();
        }

        private IObservable<IEnumerable<string>> GetResults(string term)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://en.wikipedia.org/w/api.php");
            return client.GetAsync($"?action=opensearch&format=json&search={term}")
                .ToObservable()                                                         // convert the .NET Async (Task<T>) to an IObservable<T>
                .SelectMany(response => response.Content.ReadAsStringAsync())           // now we can do cool stuff like flatten async calls
                .Select(responseString => (JArray.Parse(responseString)[1] as JArray).Values<string>());   // and transform the response
        }

        private IDisposable PopulateListFromObservable(IObservable<IEnumerable<string>> ob)
        {
            return ob.ObserveOn(SynchronizationContext.Current)      // IMPORTANT: switch back to the UI thread, otherwise we'll get errors or see no updates.
                .Subscribe(results =>
                {                                               // finally, when we get the results back:
                    listBox1.Items.Clear();                     // clear the list
                    listBox1.Items.AddRange(results.ToArray()); // append the new results. 
                });
        }

        private void Form1_Load(object sender, EventArgs ev)
        {   
            var keyup = Observable.FromEventPattern(txtSearch, nameof(KeyUp))     // listen to KeyUp events on the textbox
                .Select(e => txtSearch.Text)                    // when it fires, grab the text from the textbox
                .Where(text => text.Length > 2)                 // only continue if the term is > 2 characters
                .Throttle(TimeSpan.FromMilliseconds(750))       // Throttle to every 750ms
                .DistinctUntilChanged()                         // only fire if it's changed
                .SelectMany(GetResults);                        // get the results from wikipedia


            var doubleClick = Observable.FromEventPattern(listBox1, nameof(DoubleClick))
                .Select(x => listBox1.SelectedItem.ToString())
                .Do(x => txtSearch.Text = x)
                .SelectMany(GetResults);
                

            _subscription1 = PopulateListFromObservable(keyup);
            _subscription2 = PopulateListFromObservable(doubleClick);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _subscription1.Dispose();       // Rx.NET makes use of the IDisposable at the heart of .NET
            _subscription2.Dispose();
        }
    }
}
