using System;
using Arthur.LmuOverlay;

// Fixture generator for automated verification and the attached preview only.
// This file is excluded from the Windows application.
static class PreviewData
{
    public static Frame Create(double time,int playerPosition=13)
    {
        double phase=time%10;
        double brake=phase>=6 && phase<8 ? Math.Pow(1-(phase-6)/2,1.6)*.97 : 0;
        double throttle=phase<6 ? Math.Min(1,phase/1.2) : phase>8 ? (phase-8)/2 : 0;
        var f=new Frame { Connected=true,HasTelemetry=true,Demo=true,InRealtime=true,Track="Circuit de la Sarthe",Session=10,Phase=5,
            Status="Na pista • 47 pilotos",PlayerClass="LMGT3",Remaining=3027,Elapsed=time,Throttle=throttle,Brake=brake,
            Rpm=4400+throttle*3700-brake*800,MaxRpm=8500,MaxGears=6,Gear=4,Speed=231,Fuel=42.6,Capacity=90,
            Steering=-.18,SteeringRange=540,VehicleModel="GT3" };
        string[] names={"L. Moreau","M. Rossi","D. Weber","R. Almeida","J. Sato","S. Martin","G. Ferreira","A. Costa","E. Dubois","T. Müller","C. Silva","V. Santos","Arthur Gabriel","M. Oliveira","A. López","P. Laurent","N. Clarke","R. Pereira","F. Bianchi","D. Kim","S. Wilson","M. Tanaka","B. Lima","E. Costa"};
        string[] countries={"fr","it","de","br","jp","gb","pt","br","fr","de","br","br","br","pt","es","fr","gb","br","it","kr","us","jp","br","pt"};
        string[] classes={"HYPERCAR","LMP2","LMGT3"};int[] counts={15,8,24};int id=1;
        string[] cars={"Porsche 911 GT3 R","Ferrari 296 GT3","BMW M4 GT3","Aston Martin Vantage GT3","McLaren 720S GT3","Corvette Z06 GT3","Lexus RC F GT3","Ford Mustang GT3"};
        string[] hyper={"Porsche 963","Ferrari 499P","BMW M Hybrid V8","Alpine A424","Toyota GR010"};
        for(int c=0;c<classes.Length;c++) for(int i=0;i<counts[c];i++,id++)
        {
            int gain=i%5==0 ? -2 : i%3==0 ? 3 : 0;
            f.Drivers.Add(new Driver { Id=id,Class=classes[c],Position=id,ClassPosition=i+1,StartClassPosition=Math.Max(1,i+1+gain),
                Name=c==2 && i+1==playerPosition ? "Arthur Gabriel" : names[(i+c*5)%names.Length].Replace("Arthur Gabriel","J. Cardoso"),
                Country=c==2 && i+1==playerPosition ? "br" : countries[(i+c*5)%names.Length],IsPlayer=c==2 && i+1==playerPosition,
                VehicleKey="car-"+id,CarName=c==0 ? hyper[i%hyper.Length] : c==1 ? "Oreca 07" : cars[i%cars.Length],
                TyreFront=i%6==0 ? "Wet" : i%3==0 ? "Hard" : i%3==1 ? "Medium" : "Soft",
                TyreRear=i%6==0 ? "Wet" : i%3==0 ? "Hard" : i%3==1 ? "Medium" : "Soft",TyreSource="teste",
                Penalties=i==10 ? 1 : i==14 ? 2 : 0,PenaltyTypes=i==10 ? "DT" : i==14 ? "SG" : "",
                LastLap=207.413+c*12+i*.241,BestLap=206.008+c*12+i*.143,Gap=(id-1)*2.417,Laps=14,
                PitStops=1,InPits=i==3 });
        }
        return f;
    }
}
