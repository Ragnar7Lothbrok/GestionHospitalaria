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
        public int Prioridad { get; set; }

        public Paciente(int id, int llegadaHospital, int tiempoConsulta, int tiempoLlegada, bool requiereDiagnostico, int prioridad)
        {
            Id = id;
            LlegadaHospital = llegadaHospital;
            TiempoConsulta = tiempoConsulta;
            Estado = "EsperaConsulta"; // Estado inicial
            TiempoLlegada = tiempoLlegada;
            RequiereDiagnostico = requiereDiagnostico;
            Prioridad = prioridad;
        }
    }

    internal class Program
    {
        static PriorityQueue<Paciente, int> colaPacientes = new PriorityQueue<Paciente, int>();
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
        static int[] pacientesAtendidos = new int[3];
        static int[] totalTiempoEspera = new int[3];
        static int totalDiagnosticos = 0;
        static int totalTiempoUsoMaquinas = 0; 

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
            MostrarEstadisticas();
        }

        static void GenerarPacientes()
        {
            int numeroPaciente = 1;

            while (numeroPaciente <= 20)
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
                int prioridad = random.Next(1, 4);

                Paciente nuevoPaciente = new Paciente(idAleatorio, numeroPaciente, tiempoConsulta, tiempoLlegada, requiereDiagnostico, prioridad);
                
                Console.WriteLine($"Paciente {nuevoPaciente.Id}. Llegado el {nuevoPaciente.LlegadaHospital}. Prioridad: {nuevoPaciente.Prioridad}. Estado: {nuevoPaciente.Estado}. Requiere diagnóstico: {nuevoPaciente.RequiereDiagnostico}");
                
                lock (colaPacientes)
                    {
                        colaPacientes.Enqueue(nuevoPaciente, nuevoPaciente.Prioridad); 
                    } 

                numeroPaciente++;
                Thread.Sleep(2000);
            }
        }

        static void AtenderPacientes()
        {
            while (true)
            {
                Paciente? paciente = null;
                int prioridad = 0;

                lock (colaPacientes)
                {
                    colaPacientes.TryDequeue(out paciente, out prioridad);
                }

                if (paciente != null)
                {
                    int numeroMedico = random.Next(0, 4);
                    medicos[numeroMedico].Wait();

                    try
                    {
                        paciente.Estado = "Consulta";
                        int tiempoEspera = (int)(cronometro.ElapsedMilliseconds / 1000) - paciente.TiempoLlegada;
                        totalTiempoEspera[paciente.Prioridad - 1] += tiempoEspera;
                        pacientesAtendidos[paciente.Prioridad - 1]++;

                        Console.WriteLine($"Paciente {paciente.Id}. Prioridad: {paciente.Prioridad}. Estado: {paciente.Estado}. Atendido por el Médico {numeroMedico + 1}.");
                        Thread.Sleep(paciente.TiempoConsulta);

                        if (paciente.RequiereDiagnostico)
                        {
                            paciente.Estado = "EsperaDiagnostico";
                            colaDiagnostico.Add(paciente);
                        }
                        else
                        {
                            paciente.Estado = "Finalizado";
                            Console.WriteLine($"Paciente {paciente.Id}. Prioridad: {paciente.Prioridad}. Estado: {paciente.Estado}. Consulta completada.");
                        }
                    }
                    finally
                    {
                        medicos[numeroMedico].Release();
                    }
                }
                Thread.Sleep(500);
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
                    totalDiagnosticos++;
                    totalTiempoUsoMaquinas += 15;

                    Console.WriteLine($"Paciente {paciente.Id}. Estado: {paciente.Estado}. Usando máquina de diagnóstico {numeroMaquina + 1}.");
                    Thread.Sleep(15000); 
                    paciente.Estado = "Finalizado";
                    Console.WriteLine($"Paciente {paciente.Id}. Prioridad: {paciente.Prioridad}. Estado: {paciente.Estado}. Diagnóstico completado.");
                }
                finally
                {
                    maquinasDiagnostico[numeroMaquina].Release();
                }
            }
        }

        static void MostrarEstadisticas()
        {
            Console.WriteLine("\n--- FIN DEL DÍA ---\nPacientes atendidos:");
            Console.WriteLine($"- Emergencias: {pacientesAtendidos[0]}");
            Console.WriteLine($"- Urgencias: {pacientesAtendidos[1]}");
            Console.WriteLine($"- Consultas generales: {pacientesAtendidos[2]}\n");

            Console.WriteLine("Tiempo promedio de espera:");
            for (int i = 0; i < 3; i++)
            {
                int promedio = pacientesAtendidos[i] > 0 ? totalTiempoEspera[i] / pacientesAtendidos[i] : 0;
                Console.WriteLine($"- Prioridad {i + 1}: {promedio}s");
            }

            double usoMaquinas = (totalTiempoUsoMaquinas / (2.0 * cronometro.Elapsed.TotalSeconds)) * 100;
            Console.WriteLine($"\nUso promedio de máquinas de diagnóstico: {usoMaquinas:F2}%");
        }
    }
}
