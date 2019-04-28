# reqit
A Data Generator and Service Virtualiser

The release contains platform independent binaries (.NET Core) and will run on
Windows, Mac or Linux (or under Docker).

## Introduction
Driven by a single YAML file, you define entities and API endpoints that are used for
synthetic data generation and REST API simulation. Data persistence is also supported
so you can implement a fully working Create-Read-Update-Delete (CRUD) API if you wish.

Entities can be added either manually or by uploading JSON retrieved from real APIs and
data can be generated from entities in JSON, SQL (insert statements) or CSV format.

Many functions are available to help with data generation and data can be completely
random or can be randomly selected from either the supplied sample files or from your
own custom sample files. Within a sample file, you can give each sample a gender and/or
a rarity score to help with data generation and to create consistentency (matching gender)
across entity instances, e.g. to associate a feminine title with a feminine first name.

reqit is designed to be run either locally or remotely, e.g. under Docker. This is achieved
by providing a "command-line as a service" option where anything that you can do by
calling reqit directly from the command-line can also be done via an API call. For example,

```
reqit run --admin
```

will run reqit as a server and will enable "command-line as a service" mode. You can then
call, e.g.

```
GET http://<server>:5000/?cmd=--help
```

to request help. This is the equivalent of calling

```
reqit --help
```

To supply input to "command-line as a service", use POST instead of GET and include a body.
This is the equivalent of suppying the `-i <inputfile>` parameter.

## Pre-requisites
The only pre-requisite is ASP.NET Core/.NET Core 2.2 Runtime which can be downloaded from here:

https://dotnet.microsoft.com/download/dotnet-core/2.2

or use a Package Manager to install the pre-requisites on Linux. See:

```
dotnet-runtime-2.2.4-ubuntu.sh
```

You can also run under Docker. The source includes Dockerfile and docker-compose.yml files.

## Quick Start
A few commands are available to get you up and running quickly.

To generate an example CRUD API you can use the write command which adds data to the YAML file
(or creates a new file if it doesn't already exist). First create your entity as a JSON file, e.g.

```
echo {id: "func.num(4)", name: "func.sample(firstname)"} > person.json
```

Now type:

```
reqit write -e person -m ~crud -i person.json
```

If you now view the reqit.yaml file it will contain an entity called 'person', an alias called
'person_list' and a complete set of API endpoints that use it.

Test your entity with one of the following commands:

```
reqit read -e person
reqit read -e person_list
```

Test your API with the following commands. Note that this API has persistence so the first call
will return a "not found" error as you have to create some data before you can retrieve it.

```
reqit call -m get -p /persons/1
echo {name: "Bob"} > newperson.json
reqit call -m put -p /persons/1 -i newperson.json
reqit call -m get -p /persons/1
echo {name: "Betty"} > newperson.json
reqit call -m post -p /persons -i newperson.json
reqit call -m get -p /persons
reqit call -m delete -p /persons/1
```

Note that instead of using the "call" command you can actually call the API if reqit is running
as a server. Try the following:

```
reqit run
```

Now try the following in Postman (or use a browser if you just want to do a GET):

```
GET http://localhost:5000/persons
```

To generate SQL insert statements for your new entity, try the following:

```
reqit read -e person_list --sql
```

You can also replace `--sql` with `--csv` to generate CSV data instead and you can redirect the
output to a file by adding `-o persons.txt`.

Finally, try converting your API from persisted to generated. To do this, edit the reqit.yaml
file and remove all the `persist:` lines. Now try the following:

```
reqit call -m get -p /persons
```

Notice that you now always get 5 person objects in the output as this is the number defined in the
alias entry. With persistence, the 5 is ignored and you get however many objects are actually persisted.

All persisted data is stored in the reqit/persist folder. You can delete this folder if you wish to
remove all persisted data or you can run `reqit persist --def=person_{id} --delete` to delete a specific
set of entities.

If you want further examples of how to use the many available functions when generating data you
can create a 'sample' entity by typing the following:

```
reqit write -e ~sample
```

This adds an entity called 'sample' to reqit.yaml. You can generate a set of data from this sample
by typing:

```
reqit read -e sample
```

Each time you do this it will generate a new set of data.

## Documentation

All documentation is included within reqit. Start by typing:

```
reqit --help
```

You can get help on all the available functions by typing:

```
reqit --funchelp
```

If you load the supplied reqit.postman_collection.json and reqit.postman_environment.json files
into Postman this will help you greatly as it includes many of the available "command-line as a
service" commands as well as a selection of API calls to test your Virtual Service.

To understand the reqit.yaml file, examine the one included in the initial image. You can
re-create this at any time by deleting your reqit.yaml and then running `Command - Write Employee CRUD`
from the supplied Postman collection. Notice that the defined employee entity is used in the
various API endpoint definitions but has modifications applied to it. For example, the POST
call modifies its request as follows:

```
request: employee, !id
```

This modification takes the employee entity but excludes the id attribute, because the id
shouldn't be supplied as it is generated and returned in the response.

Another example is the GET /employees/{id} call. This modifies the response so that, rather than
generating an id it returns the same id that was passed in the endpoint path.

```
response: employee, id=~path.id
```

Notice that a tilde is used on the right hand side of the assignment to indicate that you are
referencing another attribute. If there was no tilde, the right hand side would be a literal.

Finally, you can include a wildcard in the modification to replace many attributes in one go.
So, in the POST call, all attributes that were supplied in the request are mirrored in the response:

```
response: employee, *=~request
```

Notice that the POST request excludes the id, therefore the id returned in the response will be
the one defined in the employee entity, i.e.

```
id: STR func.num(4)
```

so it will be generated.

# reqit_mon
An optional program "reqit monitor" is included to help you compose your reqit.yaml file when
reqit is running remotely.
