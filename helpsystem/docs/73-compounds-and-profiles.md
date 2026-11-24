# Compounds and profiles

A compound finds its profile in the database by its CAS number or by its name. Every compound of the simulation needs a profile. When one does not have it, the calculation stops with a message that names the compound; remove it from the simulation or use another property package.

A compound can also carry its own profile, which takes precedence over the database. A profile is built from a GAMESS output file made as LVPP makes its own (the instructions are in the repository of the database) and stored in the compound through a script:

    DWSIM.Extensions.PropertyPackages.CosmoSac.SigmaProfileDatabase
        .ImportGamessOutput(compound, path)

