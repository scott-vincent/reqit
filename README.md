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

reqit run --admin

will run reqit as a server and will enable "command-line as a service" mode. You can then
call, e.g. "GET \<server>:5000/?cmd=--help" to request help.

To supply input to the command line, use POST instead of GET and include a body.

## Pre-requisites
The only pre-requisite is ASP.NET Core/.NET Core 2.2 Runtime which can be downloaded from here:

https://dotnet.microsoft.com/download/dotnet-core/2.2

or use a Package Manager to install the pre-requisites on Linux. See:

dotnet-runtime-2.2.4-ubuntu.sh

You can also run under Docker. The source includes Dockerfile and docker-compose.yml files.

## Quick Start
A few commands are available to get you up and running quickly.

To generate an example CRUD API you can use the write command which adds data to the YAML file
(or creates a new file if it doesn't already exist). First create your entity as a JSON file, e.g.

  echo {id: "func.num(4)", name: "func.sample(firstname)"} > myentity.json

Now type:

  reqit write -e myentity -m ~crud -i myentity.json

If you now view the reqit.yaml file it will contain an entity called 'myentity' and a complete
set of API endpoints that use it.

To generate a sample entity that shows how to use many of the available functions, type:

  reqit write -e ~sample

This adds an entity called 'sample' to reqit.yaml. You can generate a set of data from this sample
by typing:

  reqit read -e sample

Each time you do this it will generate a new set of data.

If you want to generate SQL insert statments for 10 samples, edit the reqit.yaml file and add a new
section:

  alias:
    sample_set: "[sample, 10]"
  
and then type:

  reqit read -e sample_set -sql
  
You can redirect this output to a file by adding "-o sample_set.sql" to the above command.

## Documentation

All documentation is included within reqit. Start by typing:

reqit --help

You can get help on all the available functions by typing:

reqit --funchelp

If you load the supplied reqit.postman_collection.json and reqit.postman_environment.json files
into Postman this will help you greatly as it includes many of the available "command-line as a
service" commands as well as a selection of API calls to test your Virtual Service.

To understand the reqit.yaml file, examine the one included in the initial image. You can
re-create this at any time by deleting your reqit.yaml and then running "Command - Write Employee CRUD"
from the supplied Postman collection. Notice that the defined employee entity is used in the
various API endpoint definitions but has modifications applied to it. For example, the POST
call modifies its request as follows:

  request: employee, !id
  
This modification takes the employee entity but excludes the id attribute, because the id
shouldn't be supplied as it is generated and returned in the response.

Another example is the GET /employees/{id} call. This modifies the response so that, rather than
generating an id it returns the same id that was passed in the endpoint path.

  response: employee, id=~path.id
  
Notice that a tilde is used on the right hand side of the assignment to indicate that you are
referencing another attribute. If there was no tilde, the right hand side would be a literal.

Finally, you can include a wildcard in the modification to replace many attributes in one go.
So, in the POST call, all attributes that were supplied in the request are mirrored in the response:

  response: employee, *=~request

Notice that the POST request excludes the id, therefore the id returned in the response will be
the one defined in the employee entity, i.e.

  id: STR func.num(4)
  
so it will be generated.

# reqit_mon
An optional program "reqit monitor" is included to help you compose your reqit.yaml file when
reqit is running remotely.

