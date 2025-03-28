using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace GestionHospitalaria
{
    public enum EstadoPaciente
    {
        EsperaConsulta,
        Consulta,
        EsperaDiagnostico,
        Diagnostico,
        Finalizado
    }

    public class Paciente
    {
        public int Id { get; set; }
        public int LlegadaHospital { get; set; }
        public int TiempoConsulta { get; set; }
        public string Estado { get; set; }
        public int TiempoLlegada { get; set; }
        public bool RequiereDiagnostico { get; set; }

        public Paciente(int id, int llegadaHospital, int tiempoConsulta, int tiempoLlegada, bool requiereDiagnostico)
        {
            Id = id;
            LlegadaHospital = llegadaHospital;
            TiempoConsulta = tiempoConsulta;
            Estado = "EsperaConsulta"; // Estado inicial
            TiempoLlegada = tiempoLlegada;
            RequiereDiagnostico = requiereDiagnostico;
        }
    }

    internal class Program
    {
        static BlockingCollection<Paciente> colaPacientes = new BlockingCollection<Paciente>(20);
        static BlockingCollection<Paciente> colaDiagnostico = new BlockingCollection<Paciente>();
        static SemaphoreSlim[] medicos = new SemaphoreSlim[4]
        {
            new SemaphoreSlim(1),
            new SemaphoreSlim(1),
            new SemaphoreSlim(1),
            new SemaphoreSlim(1)
        };
        static SemaphoreSlim[] maquinasDiagnostico = new SemaphoreSlim[2]
        {
            new SemaphoreSlim(1),
            new SemaphoreSlim(1)
        };
        static Random random = new Random();
        static Stopwatch cronometro = Stopwatch.StartNew();
        static HashSet<int> idsGenerados = new HashSet<int>();

        static void Main(string[] args)
        {
            Console.WriteLine("--- Consulta Médica: Unidades de Diagnóstico ---");

            Task.Factory.StartNew(() => GenerarPacientes());

            for (int i = 1; i <= 4; i++)
            {
                Task.Factory.StartNew(() => AtenderPacientes());
            }
            Task.Factory.StartNew(() => AtenderDiagnosticos(0));
            Task.Factory.StartNew(() => AtenderDiagnosticos(1));

            Console.ReadLine();
        }

        static void GenerarPacientes()
        {
            int numeroPaciente = 1;

            while (numeroPaciente <= 4)
            {
                int idAleatorio;
                lock (random) 
                {
                    do
                    {
                        idAleatorio = random.Next(1, 101); 
                    }
                    while (!idsGenerados.Add(idAleatorio));
                }
                int tiempoConsulta = random.Next(5000, 15001);
                int tiempoLlegada = (int)(cronometro.ElapsedMilliseconds / 1000);
                bool requiereDiagnostico = random.Next(0, 2) == 1;

                Paciente nuevoPaciente = new Paciente(idAleatorio, numeroPaciente, tiempoConsulta, tiempoLlegada, requiereDiagnostico);
                
                Console.WriteLine($"Paciente {nuevoPaciente.Id}. Llegado el {nuevoPaciente.LlegadaHospital}. Estado: {nuevoPaciente.Estado}. Duración Espera: {nuevoPaciente.TiempoLlegada} segundos. Requiere diagnóstico: {nuevoPaciente.RequiereDiagnostico}");
                
                Thread.Sleep(1000);
                colaPacientes.Add(nuevoPaciente);

                numeroPaciente++;
                Thread.Sleep(2000);
            }

            colaPacientes.CompleteAdding();
        }

        static void AtenderPacientes()
        {
            foreach (var paciente in colaPacientes.GetConsumingEnumerable())
            {
                int numeroMedico;

                lock (random)
                {
                    numeroMedico = random.Next(0, 4);
                }

                medicos[numeroMedico].Wait();

                try
                {
                    paciente.Estado = "Consulta";
                    Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Atendido por el Médico {numeroMedico + 1}.");
                    Thread.Sleep(paciente.TiempoConsulta);

                    if (paciente.RequiereDiagnostico)
                    {
                        paciente.Estado = "EsperaDiagnostico";
                        Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Consulta finalizada.");
                        
                        colaDiagnostico.Add(paciente);
                    }
                    else
                    {
                        paciente.Estado = "Finalizado";
                        Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Consulta finalizada.");
                    }
                }
                finally
                {
                    medicos[numeroMedico].Release();
                }
            }
        }
        static void AtenderDiagnosticos(int numeroMaquina)
        {
            foreach (var paciente in colaDiagnostico.GetConsumingEnumerable())
            {
                maquinasDiagnostico[numeroMaquina].Wait();
                try
                {
                    paciente.Estado = "Diagnostico";
                    Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Usando máquina de diagnóstico {numeroMaquina + 1}.");
                    Thread.Sleep(15000); // 15 segundos de diagnóstico
                    paciente.Estado = "Finalizado";
                    Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Diagnóstico finalizado.");
                }
                finally
                {
                    maquinasDiagnostico[numeroMaquina].Release();
                }
            }
        }
    }
}
