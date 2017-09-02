﻿using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using servicehost;
using servicehost.nonpublic;
using servicehost.nancy;
using RestSharp;
using System.IO;
using System.Text;
using Nancy;
using servicehost.nancy.nonpublic;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace servicehost_tests
{
    [TestFixture]
    public class test_NancyHosting
    {
        [Test]
        public void Start_request_stop()
        {
            using (var sut = new NancyHosting()) {
                Console.WriteLine("Started...");
                sut.Start(new Uri("http://localhost:1234"), new ServiceInfo[0]);
                Console.WriteLine("Started!");

                var result = new WebClient().DownloadString("http://localhost:1234/acceptancetest_helloworld");
                Console.WriteLine("Called...");
                Assert.AreEqual("Hello World!", result);
                Console.WriteLine("Received: {0}", result);
            }
        }

        [Test]
        public void Host_hard_coded_service() { 
            using (var sut = new NancyHosting())
            {
                Console.WriteLine("Started...");
                sut.Start(new Uri("http://localhost:1234"), new[] { 
                    new ServiceInfo{
                        ServiceType = typeof(NancyHostingTestService),
                        EntryPointMethodname = "Echo",
                        Route = "/echo_querystring",
                        HttpMethod = HttpMethods.Get,
                        Parameters = new[]{ new ServiceParameter { Name = "ping", Type = typeof(string)}},
                        ResultType = typeof(string)
                    }
                });
                Console.WriteLine("Started with service!");

                var cli = new WebClient();
                var result = cli.DownloadString("http://localhost:1234/echo_querystring?ping=$data");

                Console.WriteLine("Called...");
                Assert.AreEqual(new[] { "handle" }, NancyHostingTestService.Log.ToArray());
                Assert.AreEqual("42", result);
                Console.WriteLine("Received: {0}", result);
            }
        }

        [Test]
        public void Host_hard_coded_service_with_setup_and_teardown()
        {
            using (var sut = new NancyHosting())
            {
                Console.WriteLine("Started...");
                sut.Start(new Uri("http://localhost:1234"), new[] {
                    new ServiceInfo{
                        ServiceType = typeof(NancyHostingTestService),
                        EntryPointMethodname = "Echo",
                        SetupMethodname = "Setup",
                        TeardownMethodname = "Cleanup",
                        Route = "/echo_payload",
                        HttpMethod = HttpMethods.Get,
                        Parameters = new[]{ new ServiceParameter { Name = "ping", Type = typeof(string)}},
                        ResultType = typeof(string)
                    }
                });
                Console.WriteLine("Started with service!");

                var cli = new WebClient();
                var result = cli.DownloadString("http://localhost:1234/echo_payload?ping=hello");

                Console.WriteLine("Called...");
                Assert.AreEqual(new[] { "setup", "handle", "cleanup" }, NancyHostingTestService.Log.ToArray());
                Assert.AreEqual("hello", result);
                Console.WriteLine("Received: {0}", result);
            }
        }


        [Test]
        public void Host_hard_coded_service_with_exception()
        {
            using (var sut = new NancyHosting())
            {
                Console.WriteLine("Started...");
                sut.Start(new Uri("http://localhost:1234"), new[] {
                    new ServiceInfo{
                        ServiceType = typeof(NancyHostingTestService),
                        EntryPointMethodname = "XYZ",
                        Route = "/echo_querystring",
                        HttpMethod = HttpMethods.Get
                    }
                });
                Console.WriteLine("Started with service!");

                try
                {
                    var cli = new WebClient();
                    cli.DownloadString("http://localhost:1234/echo_querystring?ping=$data");
                }
                catch (WebException webex) {
                    var resp = (HttpWebResponse)webex.Response;
                    Assert.AreEqual(HttpStatusCode.InternalServerError, resp.StatusCode);

                    var readStream = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
                    var text = readStream.ReadToEnd();

                    Console.WriteLine("Expected exception:");
                    Console.WriteLine(text);
                }
            }
        }


        [Test]
        public void Host_same_route_with_different_verbs()
        {
            using (var sut = new NancyHosting())
            {
                Console.WriteLine("Started...");
                sut.Start(new Uri("http://localhost:1234"), new[] {
                    new ServiceInfo{
                        ServiceType = typeof(NancyHostingTestService),
                        EntryPointMethodname = "ToUpper",
                        Route = "/changecase",
                        HttpMethod = HttpMethods.Get,
                        Parameters = new[]{ new ServiceParameter { Name = "text", Type = typeof(string)}},
                        ResultType = typeof(string)
                    },
                    new ServiceInfo{
                        ServiceType = typeof(NancyHostingTestService),
                        EntryPointMethodname = "ToLower",
                        Route = "/changecase",
                        HttpMethod = HttpMethods.Post,
                        Parameters = new[]{ new ServiceParameter { Name = "text", Type = typeof(string)}},
                        ResultType = typeof(string)
                    }
                });
                Console.WriteLine("Started with service!");

                var cli = new WebClient();
                var result = cli.DownloadString("http://localhost:1234/changecase?text=hello");
                Console.WriteLine("Called...");
                Assert.AreEqual(new[] { "ViaGet" }, NancyHostingTestService.Log.ToArray());
                Assert.AreEqual("HELLO", result);
                Console.WriteLine("Received: {0}", result);

                result = cli.UploadString("http://localhost:1234/changecase?text=HELLO", "");
                Console.WriteLine("Called...");
                Assert.AreEqual(new[] { "ViaPost" }, NancyHostingTestService.Log.ToArray());
                Assert.AreEqual("hello", result);
                Console.WriteLine("Received: {0}", result);
            }
        }
    }

    public class NancyHostingTestModule : NancyModule
    {
        public NancyHostingTestModule()
        {
            Get["/acceptancetest_helloworld"] = _ => "Hello World!";
        }
    }

    public class NancyHostingTestService {
        public static List<string> Log;

        public NancyHostingTestService() { 
            Log = new List<string>();
        }

        public void Setup() { Log.Add("setup"); }
        public void Cleanup() { Log.Add("cleanup"); }
        public string Echo(string ping) {
            Log.Add("handle");
            return ping.Replace("$data", "42");
        }


        public string ToUpper(string text) {
            Log.Add("ViaGet");
            return text.ToUpper();
        }

        public string ToLower(string text) {
            Log.Add("ViaPost");
            return text.ToLower();
        }
    }
}