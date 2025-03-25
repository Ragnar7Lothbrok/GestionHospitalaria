using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace GestionHospitalaria
{
    public class Paciente
    {
        public int Id { get; set; }
        public int LlegadaHospital { get; set; }
        public int TiempoConsulta { get; set; }
        public string Estado { get; set; }
        public int TiempoLlegada { get; set; }

        public Paciente(int id, int llegadaHospital, int tiempoConsulta, int tiempoLlegada)
        {
            Id = id;
            LlegadaHospital = llegadaHospital;
            TiempoConsulta = tiempoConsulta;
            Estado = "Espera"; // Estado inicial al llegar al hospital
            TiempoLlegada = tiempoLlegada;
        }
    }

    internal class Program
    {
        static BlockingCollection<Paciente> colaPacientes = new BlockingCollection<Paciente>(20);
        static SemaphoreSlim[] medicos = new SemaphoreSlim[4] 
        { 
            new SemaphoreSlim(1), // Médico 1
            new SemaphoreSlim(1), // Médico 2
            new SemaphoreSlim(1), // Médico 3
            new SemaphoreSlim(1)  // Médico 4
        };
        static Random random = new Random();
        static Stopwatch cronometro = Stopwatch.StartNew();
        static HashSet<int> idsGenerados = new HashSet<int>(); // Nos aseguramos que los IDs sean únicos

        static void Main(string[] args)
        {
            Console.WriteLine("--- Consulta Médica ---");

            // Iniciar el hilo productor (pacientes que llegan)
            Task.Factory.StartNew(() => GenerarPacientes());

            // Iniciar los hilos consumidores (médicos que atienden)
            for (int i = 1; i <= 4; i++)
            {
                Task.Factory.StartNew(() => AtenderPacientes());
            }

            Console.ReadLine(); // Esperar a que el usuario pulse Enter para terminar
        }

        static void GenerarPacientes()
        {
            int numeroPaciente = 1;

            while (numeroPaciente <= 4) // Generamos solo 4 pacientes para esta simulación
            {
                int idAleatorio;
                lock (random) 
                {
                    do
                    {
                        idAleatorio = random.Next(1, 101); 
                    }
                    while (!idsGenerados.Add(idAleatorio)); // Añade el ID si es único, sino genera otro.
                }
                int tiempoConsulta = random.Next(5000, 15001); // Tiempo en consulta aleatorio entre 5 y 15 segundos
                int tiempoLlegada = (int)(cronometro.ElapsedMilliseconds / 1000);

                Paciente nuevoPaciente = new Paciente(idAleatorio, numeroPaciente, tiempoConsulta, tiempoLlegada);
                
                Console.WriteLine($"Paciente {numeroPaciente} con ID: {nuevoPaciente.Id} llega al hospital y espera ser atendido. Tiempo de llegada: {nuevoPaciente.TiempoLlegada} segundos.");
                colaPacientes.Add(nuevoPaciente);

                numeroPaciente++;
                Thread.Sleep(2000); // Simular llegada cada 2 segundos
            }

            colaPacientes.CompleteAdding(); // Indicar que no se generarán más pacientes
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
                    Console.WriteLine($"Médico {numeroMedico + 1} está atendiendo al Paciente {paciente.LlegadaHospital} (ID: {paciente.Id}).");

                    Thread.Sleep(paciente.TiempoConsulta); // Tiempo de consulta aleatorio

                    paciente.Estado = "Finalizado";
                    Console.WriteLine($"Paciente {paciente.LlegadaHospital} (ID: {paciente.Id}) ha sido atendido por el Médico {numeroMedico + 1}. Estado: {paciente.Estado}");
                }
                finally
                {
                    medicos[numeroMedico].Release();
                }
            }
        }
    }
}
