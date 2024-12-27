using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using utils;
using onvif;
using onvif.services;
using odm.core;
using System.Net;
using System.Reactive.Disposables;
using onvif.utils;

namespace EventsConsole {
	class Program {
		static readonly string preff = @"http://";
		static readonly string postf = @"/onvif/device_service";
		static void Main(string[] args) {
			if (!args.Any()) {
				Console.WriteLine("Please run application whith KipodServer's ip.address as argument. ");
				Console.WriteLine("If server has user account - enter user name password. ");
				Console.WriteLine("Compleate arumets string example: 192.168.0.2 admin pass123");
				Console.ReadKey();
				return;
			}

			NetworkCredential account = null;
			if (args.Length == 3) {
 				account = new NetworkCredential(args[1], args[2]);
			}

			string uriStr = address(args[0]);
			Uri uri;
			if (!Uri.TryCreate(uriStr, UriKind.Absolute, out uri)) {
				Console.WriteLine("Uri string is in incorrect format! " + uriStr);
				Console.ReadKey();
			}

			SampleRunner runner = new SampleRunner(uri, account);

			Console.WriteLine("Press spase to exit...");
			while (Console.ReadKey().Key != ConsoleKey.Spacebar) {
				Console.WriteLine("Press spase to exit...");
			}

			runner.Dispose();
		}
		static string address(string ip) {
			return preff + ip + postf;
		}

		public class SampleRunner: IDisposable {
			public SampleRunner(Uri uri, NetworkCredential account) {
				// TODO: Complete member initialization
				
				Init(uri, account);
			}
			CompositeDisposable disposables = new CompositeDisposable();
			
			private void Init(Uri uri, NetworkCredential account) {
				NvtSessionFactory factory = new NvtSessionFactory(account);

				disposables.Add(factory.CreateSession(new[] { uri })
					.Subscribe(
					session => {
						disposables.Add(new EventManager(session));
                        disposables.Add(new ConfigurationEditor(session));
					}, err => {
						Console.WriteLine(err.Message);
					}
				));
			}

			public void Dispose() {
				disposables.Dispose();
			}
		}

        public class ConfigurationEditor : IDisposable {
            private INvtSession session;
            onvif.utils.OdmSession facade;
            CompositeDisposable disposables = new CompositeDisposable();
            public ConfigurationEditor(INvtSession session) {
                this.session = session;
                RunConfigChanges();
            }
            void RunConfigChanges() {
                disposables.Add(
                    session.GetDeviceInformation()
                    .Subscribe(di => {
                        Console.WriteLine();
                        Console.WriteLine("Device information:");
                        Console.WriteLine("firmware     - " + di.FirmwareVersion);
                        Console.WriteLine("hardware     - " + di.HardwareId);
                        Console.WriteLine("manufacturer - " + di.Manufacturer);
                        Console.WriteLine("model        - " + di.Model);
                        Console.WriteLine("serial number- " + di.SerialNumber);
                        Console.WriteLine();
                    }, err => {
                        Console.WriteLine(err.Message);
                    }));
                disposables.Add(session.GetScopes()
                    .Subscribe(sc => {
                        var stringScopes = sc.Select(s => s.scopeItem);

                        DisplayNameLocation(stringScopes.ToArray());

			//Uncomment this to change name/location
                        //SetNewNameLocation();
                    }, err => {
                        Console.WriteLine(err.Message);
                    }));
            }
            void DisplayNameLocation(string[] scopes) {
                Console.WriteLine();
                Console.WriteLine("Device name/location:");
                Console.WriteLine("name         - " + ScopeHelper.GetName(scopes));
                Console.WriteLine("location     - " + ScopeHelper.GetLocation(scopes));
                Console.WriteLine();
            }
            void SetNewNameLocation() {
                facade = new onvif.utils.OdmSession(session);
                
                disposables.Add(facade.SetNameLocation("New Name", "New Location")
                    .Subscribe(success=>{
                        Console.WriteLine("-------------------------");
                        Console.WriteLine("New name/ location setted");
                        Console.WriteLine("-------------------------");

                        disposables.Add(session.GetScopes()
                            .Subscribe(sc => {
                                var stringScopes = sc.Select(s => s.scopeItem);
                                Console.WriteLine("");
                                Console.WriteLine("Checkng new name:");
                                Console.WriteLine("");
                                DisplayNameLocation(stringScopes.ToArray());
                   }, err => {
                       Console.WriteLine(err.Message);
                   }));

                    }, err=>{
                        Console.WriteLine(err.Message);
                    }));
            }
            public void Dispose() {
            }
        }

		public class EventManager: IDisposable {
			public EventManager(INvtSession session) {
				// TODO: Complete member initialization
				this.session = session;
				
				Run();
			}

			private INvtSession session;
			CompositeDisposable disposables = new CompositeDisposable();

			public void Dispose() {
				disposables.Dispose();
			}

			private void Run() {
				OdmSession odmSess = new OdmSession(session);

				disposables.Add(odmSess.GetPullPointEvents()
					.Subscribe(
					evnt => { 
						//Parse onvif event here
						Console.WriteLine(EventParse.ParseTopic(evnt.topic));
						var messages = EventParse.ParseMessage(evnt.message);
						messages.ForEach(msg => Console.WriteLine(msg));
						Console.WriteLine("----------------------------------------");
						Console.WriteLine();
						Console.WriteLine();
					}, err => {
						Console.WriteLine(err.Message);
					}
				));
			}
		}
		public static class EventParse{
			public static string ParseTopic(TopicExpressionType topic) {
				string topicString = "";

				topic.Any.ForEach(node => {
					topicString += "value: " + node.Value;
				});

				return topicString;
			}
			public static string[] ParseMessage(Message message) {
				List<string> messageStrings = new List<string>();

				messageStrings.Add("messge id: " + message.key);

				if(message.source!= null)
					message.source.simpleItem.ForEach(sitem => {
						string txt = sitem.name + "	" + sitem.value;
						messageStrings.Add(txt);
					});

				if (message.data != null)
					message.data.simpleItem.ForEach(sitem => {
						string txt = sitem.name + "	" + sitem.value;
						messageStrings.Add(txt);
					});

				return messageStrings.ToArray();
			}
		}
	}
}
