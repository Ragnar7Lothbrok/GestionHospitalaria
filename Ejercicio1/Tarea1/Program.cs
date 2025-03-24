using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace GestionHospitalaria
{
    internal class Program
    {
        static BlockingCollection<int> colaPacientes = new BlockingCollection<int>(20); // Máximo 20 pacientes en la sala de espera
        static SemaphoreSlim[] medicos = new SemaphoreSlim[4] 
        { 
            new SemaphoreSlim(1), // Médico 1
            new SemaphoreSlim(1), // Médico 2
            new SemaphoreSlim(1), // Médico 3
            new SemaphoreSlim(1)  // Médico 4
        };
        static Random random = new Random();

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
            int idPaciente = 1;

            while (idPaciente <= 4) // Solo generamos 4 pacientes en este ejercicio
            {
                Thread.Sleep(2000); // Paciente llega cada 2 segundos
                Console.WriteLine($"Paciente {idPaciente} llega al hospital y espera ser atendido.");

                colaPacientes.Add(idPaciente); // Añadir paciente a la cola

                idPaciente++;
            }

            colaPacientes.CompleteAdding(); // Indicar que ya no se generarán más pacientes
        }

        static void AtenderPacientes()
        {
            foreach (var paciente in colaPacientes.GetConsumingEnumerable())
            {
                int numeroMedico;

                lock (random) // Asegurarnos que el Random no sea usado por múltiples hilos al mismo tiempo
                {
                    numeroMedico = random.Next(0, 4);
                }
                medicos[numeroMedico].Wait();

                try
                {
                    Console.WriteLine($"Médico {numeroMedico + 1} está atendiendo al Paciente {paciente}.");
                    Thread.Sleep(10000); // Simular consulta de 10 segundos
                    Console.WriteLine($"Paciente {paciente} ha sido atendido por el Médico {numeroMedico + 1}.");
                }
                finally
                {
                    medicos[numeroMedico].Release(); // El médico queda disponible nuevamente
                }
            }
        }
    }
}
